// Example low level rendering Unity plugin

#include "UnityPluginInterface.h"
#include "IUnityInterface.h"
#include "IUnityGraphics.h"

#include "glew.h"

#include <math.h>
#include <stdio.h>
#include <vector>
#include <GLUT/GLUT.h>
#include <iostream>
#include <string>
#include <fstream>
#include <sstream>

#if SUPPORT_OPENGL_UNIFIED
	#if UNITY_WIN || UNITY_LINUX
		#include <GL/gl.h>
        #include "CL/cl.h"
        #include <CL/cl_gl.h>
        #define GL_SHARING_EXTENSION "cl_khr_gl_sharing"
	#else
		#include <OpenGL/OpenGL.h>
        #include "OpenCL/opencl.h"
        #include <OpenCL/cl_gl.h>
        #define GL_SHARING_EXTENSION "cl_APPLE_gl_sharing"
	#endif
#endif

static int PreErrors = -1;
cl_int error;
cl_context context;
cl_program activeProgram;
cl_uint num_devices;
cl_device_id *deviceIdList;
cl_command_queue cq;


cl_mem** voidList;

const char* kernelSource;
size_t sourceSize;

char* compilerErrorText;

class ClMem{
    public:
    void* memPtr;
    cl_mem clientMemPtr;
    int id;
    size_t size;
    size_t dsize;
    ClMem(void*,int,size_t,size_t);
    ClMem();
    ~ClMem();
    ClMem(const ClMem &obj);
    ClMem& operator=( const ClMem& other );
};
ClMem::~ClMem(void){
    //clientMemPtr = *new cl_mem;
    /*if(memPtr != NULL){
        if(fp != NULL){
            fp(memPtr);
        }
    }*/
}
ClMem::ClMem (){
    
}
ClMem::ClMem (void* mPtr,int Id,size_t Size,size_t Dsize){
    memPtr = mPtr;
    id = Id;
    size = Size;
    dsize = Dsize;
}
ClMem::ClMem (const ClMem &obj){
    memPtr = obj.memPtr;
    //clientMemPtr = *new cl_mem;
    clientMemPtr = obj.clientMemPtr;
    id = obj.id;
    size = obj.size;
    dsize = obj.dsize;
}
ClMem&ClMem::operator=( const ClMem& other ) {
    memPtr = other.memPtr;
    clientMemPtr = other.clientMemPtr;
    id = other.id;
    size = other.size;
    dsize = other.dsize;
    return *this;
}

class FrameBuffer{
public:
    GLuint glTextureID;
    cl_mem clTextureMem;
    FrameBuffer();
};
FrameBuffer::FrameBuffer (){
    
}

ClMem* clMemory = new ClMem[64];
int clMemoryIdx = 0;

FrameBuffer* fbMemory = new FrameBuffer[64];
int fbMemoryIdx = 0;
//void **memPointer = (void**)malloc(sizeof(void *) * 8);
//cl_mem * clientMemPtr = (cl_mem*)malloc(sizeof(cl_mem) * 8);
//int memLength = 0;

cl_kernel* kernels = new cl_kernel[20];
int kernelsIdx = 0;

typedef void (*FuncPtr)( const char * );
FuncPtr Debug;






/*
 unity Block
 */

static GLchar PreGLErrors[1024];

//Unity 5.2stuff
enum
{
    ATTRIB_POSITION = 0,
    ATTRIB_COLOR = 1
};
static void UNITY_INTERFACE_API OnGraphicsDeviceEvent(UnityGfxDeviceEventType eventType);

static IUnityInterfaces* s_UnityInterfaces = NULL;
static IUnityGraphics* s_Graphics = NULL;
static UnityGfxRenderer s_DeviceType = kUnityGfxRendererNull;

extern "C" void	UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API UnityPluginLoad(IUnityInterfaces* unityInterfaces)
{
    s_UnityInterfaces = unityInterfaces;
    s_Graphics = s_UnityInterfaces->Get<IUnityGraphics>();
    s_Graphics->RegisterDeviceEventCallback(OnGraphicsDeviceEvent);
    
    // Run OnGraphicsDeviceEvent(initialize) manually on plugin load
    OnGraphicsDeviceEvent(kUnityGfxDeviceEventInitialize);
}

extern "C" void UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API UnityPluginUnload()
{
    s_Graphics->UnregisterDeviceEventCallback(OnGraphicsDeviceEvent);
}

// Actual setup/teardown functions defined below
static void DoEventGraphicsDeviceGLUnified(UnityGfxDeviceEventType eventType);

static void UNITY_INTERFACE_API OnGraphicsDeviceEvent(UnityGfxDeviceEventType eventType)
{
    UnityGfxRenderer currentDeviceType = s_DeviceType;
    
    switch (eventType)
    {
        case kUnityGfxDeviceEventInitialize:
        {
            s_DeviceType = s_Graphics->GetRenderer();
            currentDeviceType = s_DeviceType;
            break;
        }
            
        case kUnityGfxDeviceEventShutdown:
        {
            s_DeviceType = kUnityGfxRendererNull;
            break;
        }
            
        case kUnityGfxDeviceEventBeforeReset:
        {
            break;
        }
            
        case kUnityGfxDeviceEventAfterReset:
        {
            break;
        }
    };
    
    if (currentDeviceType == kUnityGfxRendererOpenGLES20 ||
        currentDeviceType == kUnityGfxRendererOpenGLES30 ||
        currentDeviceType == kUnityGfxRendererOpenGLCore)
        DoEventGraphicsDeviceGLUnified(eventType);
}
struct MyVertex {
    float x, y, z;
    unsigned int color;
};
static void SetDefaultGraphicsState ();
static void DoRendering (const float* worldMatrix, const float* identityMatrix, float* projectionMatrix, const MyVertex* verts);

static void UNITY_INTERFACE_API OnRenderEvent(int eventID)
{
    // Unknown graphics device type? Do nothing.
    if (s_DeviceType == kUnityGfxRendererNull)
        return;
    
    
    // A colored triangle. Note that colors will come out differently
    // in D3D9/11 and OpenGL, for example, since they expect color bytes
    // in different ordering.
    MyVertex verts[6] = {
        { -1.0f, -1.0f,  0, 0xFFff0000 },
        {  1.0f, -1.0f,  0, 0xFFff0000 },
        { -1.0f,  1.0f,  0, 0xFFff0000 },
        
        {  1.0f,  1.0f,  0, 0xFFff0000 },
        {  1.0f, -1.0f,  0, 0xFFff0000 },
        { -1.0f,  1.0f,  0, 0xFFff0000 },
    };
    
    
    // Some transformation matrices: rotate around Z axis for world
    // matrix, identity view matrix, and identity projection matrix.
    
    float worldMatrix[16] = {
        1,0,0,0,
        0,1,0,0,
        0,0,1,0,
        0,0,0,1,
    };
    float identityMatrix[16] = {
        1,0,0,0,
        0,1,0,0,
        0,0,1,0,
        0,0,0,1,
    };
    float projectionMatrix[16] = {
        1,0,0,0,
        0,1,0,0,
        0,0,1,0,
        0,0,0,1,
    };
    
    // Actual functions defined below
    SetDefaultGraphicsState ();
    DoRendering (worldMatrix, identityMatrix, projectionMatrix, verts);
}
extern "C" UnityRenderingEvent UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API GetRenderEventFunc()
{
    return OnRenderEvent;
}
#if SUPPORT_OPENGL_UNIFIED

#define VPROG_SRC(ver, attr, varying)							\
"#version 330\n"												\
"in highp vec3 pos;\n"                                          \
"in lowp vec4 color;\n"                                         \
"\n"															\
"out lowp vec4 ocolor;\n"                                       \
"\n"															\
"uniform highp mat4 worldMatrix;\n"								\
"uniform highp mat4 projMatrix;\n"                              \
"uniform sampler2D myTextureSampler;\n"                         \
"\n"															\
"void main()\n"													\
"{\n"															\
"	gl_Position = (projMatrix * worldMatrix) * vec4(pos,1);\n"	\
"	ocolor = texture( myTextureSampler, pos.xy ).rgba;\n"									\
"}\n"															\

//texture( myTextureSampler, pos.xy ).rgba;
//static const char* kGlesVProgTextGLES2		= VPROG_SRC("\n", "attribute", "varying");
//static const char* kGlesVProgTextGLES3		= VPROG_SRC("#version 300 es\n", "in", "out");
static const char* kGlesVProgTextGLCore		= VPROG_SRC("#version 330\n", "in", "out");

#undef VPROG_SRC

#define FSHADER_SRC(ver, varying, outDecl, outVar)	\
"#version 330\n"								\
"out lowp vec4 fragColor;\n"					\
"in lowp vec4 ocolor;\n"                        \
"\n"											\
"void main()\n"									\
"{\n"											\
"	fragColor = ocolor;\n"		\
"}\n"											\

//static const char* kGlesFShaderTextGLES2	= FSHADER_SRC("\n", "varying", "\n", "gl_FragColor");
//static const char* kGlesFShaderTextGLES3	= FSHADER_SRC("#version 300 es\n", "in", "out lowp vec4 fragColor;\n", "fragColor");
static const char* kGlesFShaderTextGLCore	= FSHADER_SRC("#version 330\n", "in", "out lowp vec4 fragColor;\n", "fragColor");

#undef FSHADER_SRC

static GLuint	g_VProg;
static GLuint	g_FShader;
static GLuint	g_Program;
static GLuint	g_VertexArray;
static GLuint	g_ArrayBuffer;
static int		g_WorldMatrixUniformIndex;
static int		g_ProjMatrixUniformIndex;
static GLuint   g_shaderTextureID;

static GLuint CreateShader(GLenum type, const char* text)
{
    GLuint ret = glCreateShader(type);
    glShaderSource(ret, 1, &text, NULL);
    glCompileShader(ret);
    
    GLint success;
    glGetShaderiv(ret, GL_COMPILE_STATUS, &success);
    
    if(!success){
        glGetShaderInfoLog(ret, 1024, NULL, PreGLErrors);
    }
    
    return ret;
}

static void DoEventGraphicsDeviceGLUnified(UnityGfxDeviceEventType eventType)
{
    if (eventType == kUnityGfxDeviceEventInitialize)
    {
        /*if (s_DeviceType == kUnityGfxRendererOpenGLES20)
         {
         ::printf("OpenGLES 2.0 device\n");
         g_VProg		= CreateShader(GL_VERTEX_SHADER, kGlesVProgTextGLES2);
         g_FShader	= CreateShader(GL_FRAGMENT_SHADER, kGlesFShaderTextGLES2);
         }
         else if(s_DeviceType == kUnityGfxRendererOpenGLES30)
         {
         ::printf("OpenGLES 3.0 device\n");
         g_VProg		= CreateShader(GL_VERTEX_SHADER, kGlesVProgTextGLES3);
         g_FShader	= CreateShader(GL_FRAGMENT_SHADER, kGlesFShaderTextGLES3);
         }*/
#if SUPPORT_OPENGL_CORE
        /*else*/ if(s_DeviceType == kUnityGfxRendererOpenGLCore)
        {
            ::printf("OpenGL Core device\n");
            glewExperimental = GL_TRUE;
            glewInit();
            glGetError(); // Clean up error generated by glewInit
            
            g_VProg		= CreateShader(GL_VERTEX_SHADER, kGlesVProgTextGLCore);
            g_FShader	= CreateShader(GL_FRAGMENT_SHADER, kGlesFShaderTextGLCore);
        }
#endif
        
        glGenBuffers(1, &g_ArrayBuffer);
        glBindBuffer(GL_ARRAY_BUFFER, g_ArrayBuffer);
        glBufferData(GL_ARRAY_BUFFER, sizeof(MyVertex) * 6, NULL, GL_STREAM_DRAW);
        
        g_Program = glCreateProgram();
        glBindAttribLocation(g_Program, ATTRIB_POSITION, "pos");
        glBindAttribLocation(g_Program, ATTRIB_COLOR, "color");
        glAttachShader(g_Program, g_VProg);
        glAttachShader(g_Program, g_FShader);
#if SUPPORT_OPENGL_CORE
        if(s_DeviceType == kUnityGfxRendererOpenGLCore)
            glBindFragDataLocation(g_Program, 0, "fragColor");
#endif
        glLinkProgram(g_Program);

        GLint status = 0;
        glGetProgramiv(g_Program, GL_LINK_STATUS, &status);
        
        if(status != GL_TRUE){
            //glGetProgramInfoLog(g_Program, 1024, NULL, PreGLErrors);
        }
        
        //PreGLErrors = error;//(status != GL_TRUE? "Bad" : "Good");//std::to_string(status).c_str();
        //assert(status == GL_TRUE);
        
        g_WorldMatrixUniformIndex	= glGetUniformLocation(g_Program, "worldMatrix");
        g_ProjMatrixUniformIndex	= glGetUniformLocation(g_Program, "projMatrix");
        
       /* GLenum err = glGetError();
        char* error;
        switch(err) {
            case GL_INVALID_OPERATION:      error="INVALID_OPERATION";      break;
            case GL_INVALID_ENUM:           error="INVALID_ENUM";           break;
            case GL_INVALID_VALUE:          error="INVALID_VALUE";          break;
            case GL_OUT_OF_MEMORY:          error="OUT_OF_MEMORY";          break;
            case GL_INVALID_FRAMEBUFFER_OPERATION:  error="INVALID_FRAMEBUFFER_OPERATION";  break;
            default: error="test";
        }
        PreGLErrors = (err != GL_NO_ERROR? error :"Not");*/
        
        g_shaderTextureID  = glGetUniformLocation(g_Program, "myTextureSampler");
       /* err = glGetError();
        error = "";
        switch(err) {
            case GL_INVALID_OPERATION:      error="INVALID_OPERATION";      break;
            case GL_INVALID_ENUM:           error="INVALID_ENUM";           break;
            case GL_INVALID_VALUE:          error="INVALID_VALUE";          break;
            case GL_OUT_OF_MEMORY:          error="OUT_OF_MEMORY";          break;
            case GL_INVALID_FRAMEBUFFER_OPERATION:  error="INVALID_FRAMEBUFFER_OPERATION";  break;
            default: error="test";
        }
        PreGLErrors = (err != GL_NO_ERROR? error :"Not");*/
    }
    else if (eventType == kUnityGfxDeviceEventShutdown)
    {
        
    }
}
#endif
static void SetDefaultGraphicsState ()
{
    
#if SUPPORT_OPENGL_LEGACY
    // OpenGL 2 legacy case (deprecated)
    if (s_DeviceType == kUnityGfxRendererOpenGL)
    {
        glDisable (GL_CULL_FACE);
        glDisable (GL_LIGHTING);
        glDisable (GL_BLEND);
        glDisable (GL_ALPHA_TEST);
        glDepthFunc (GL_LEQUAL);
        glEnable (GL_DEPTH_TEST);
        glDepthMask (GL_FALSE);
    }
#endif
    
    
#if SUPPORT_OPENGL_UNIFIED
    // OpenGL ES / core case
    if (s_DeviceType == kUnityGfxRendererOpenGLES20 ||
        s_DeviceType == kUnityGfxRendererOpenGLES30 ||
        s_DeviceType == kUnityGfxRendererOpenGLCore)
    {
        glDisable(GL_CULL_FACE);
        glDisable(GL_BLEND);
        glDepthFunc(GL_LEQUAL);
        glEnable(GL_DEPTH_TEST);
        glDepthMask(GL_FALSE);
        
        //assert(glGetError() == GL_NO_ERROR);
    }
#endif
}


static void DoRendering (const float* worldMatrix, const float* identityMatrix, float* projectionMatrix, const MyVertex* verts)
{
    // Does actual rendering of a simple triangle
    
#if SUPPORT_OPENGL_LEGACY
    // OpenGL 2 legacy case (deprecated)
    if (s_DeviceType == kUnityGfxRendererOpenGL)
    {
        // Transformation matrices
        glMatrixMode (GL_MODELVIEW);
        glLoadMatrixf (worldMatrix);
        glMatrixMode (GL_PROJECTION);
        // Tweak the projection matrix a bit to make it match what identity
        // projection would do in D3D case.
        projectionMatrix[10] = 1;
        projectionMatrix[14] = 1;
        glLoadMatrixf (projectionMatrix);
        
        // Vertex layout
        glVertexPointer (3, GL_FLOAT, sizeof(verts[0]), &verts[0].x);
        glEnableClientState (GL_VERTEX_ARRAY);
        
        //glTexCoordPointer (2, GL_FLOAT, sizeof(verts[0]), &verts[0].u);
        //glEnableClientState (GL_TEXTURE_COORD_ARRAY);
        
        glColorPointer (4, GL_UNSIGNED_BYTE, sizeof(verts[0]), &verts[0].color);
        glEnableClientState (GL_COLOR_ARRAY);

        
        // Draw!
        glDrawArrays (GL_TRIANGLES, 0, 6);
        
        // update native texture from code
    }
#endif
    
    
    
#if SUPPORT_OPENGL_UNIFIED
#define BUFFER_OFFSET(i) ((char *)NULL + (i))
    
    // OpenGL ES / core case
    if (s_DeviceType == kUnityGfxRendererOpenGLES20 ||
        s_DeviceType == kUnityGfxRendererOpenGLES30 ||
        s_DeviceType == kUnityGfxRendererOpenGLCore)
    {
        //assert(glGetError() == GL_NO_ERROR); // Make sure no OpenGL error happen before starting rendering
        
        // Tweak the projection matrix a bit to make it match what identity projection would do in D3D case.
        projectionMatrix[10] = 2.0f;
        projectionMatrix[14] = -1.0f;
        
        glUseProgram(g_Program);
        glUniformMatrix4fv(g_WorldMatrixUniformIndex, 1, GL_FALSE, worldMatrix);
        glUniformMatrix4fv(g_ProjMatrixUniformIndex, 1, GL_FALSE, projectionMatrix);
        
        glActiveTexture(GL_TEXTURE0);
        glBindTexture(GL_TEXTURE_2D, fbMemory[0].glTextureID);
        
        glUniform1i(g_shaderTextureID, 0);
        
#if SUPPORT_OPENGL_CORE
        if (s_DeviceType == kUnityGfxRendererOpenGLCore)
        {
            glGenVertexArrays(1, &g_VertexArray);
            glBindVertexArray(g_VertexArray);
        }
#endif
        
        glBindBuffer(GL_ELEMENT_ARRAY_BUFFER, 0);
        glBindBuffer(GL_ARRAY_BUFFER, g_ArrayBuffer);
        glBufferSubData(GL_ARRAY_BUFFER, 0, sizeof(MyVertex) * 6, &verts[0].x);
        
        glEnableVertexAttribArray(ATTRIB_POSITION);
        glVertexAttribPointer(ATTRIB_POSITION, 3, GL_FLOAT, GL_FALSE, sizeof(MyVertex), BUFFER_OFFSET(0));
        
        glEnableVertexAttribArray(ATTRIB_COLOR);
        glVertexAttribPointer(ATTRIB_COLOR, 4, GL_UNSIGNED_BYTE, GL_TRUE, sizeof(MyVertex), BUFFER_OFFSET(sizeof(float) * 3));
        
        glDrawArrays(GL_TRIANGLES, 0, 6);
        
        
#if SUPPORT_OPENGL_CORE
        if (s_DeviceType == kUnityGfxRendererOpenGLCore)
        {
            glDeleteVertexArrays(1, &g_VertexArray);
        }
#endif
        
        //assert(glGetError() == GL_NO_ERROR);
    }
#endif
}

//end unity stuff

/*
 
 end unity Block
 */



extern "C" {
    EXPORT_API void SetDebugFunction( FuncPtr fp )
    {
        Debug = fp;
    }
}

//needs an unmanaged array.
extern "C" int EXPORT_API AllocateHostMem (void* memPtr,int size, int dsize){//TODO:template typename
    clMemory[clMemoryIdx] = *new ClMem(memPtr,clMemoryIdx,(size_t)size,(size_t) dsize) ;
    clMemoryIdx ++;
    return clMemoryIdx-1;
}

void CheckError (cl_int error,int l)
{
    //Debug(PreGLErrors);
    if (error != CL_SUCCESS) {
        Debug(getErrorString((int)error));
        Debug(((std::string)"At Line:"+std::to_string(l)).c_str());
    }
}

extern "C" int EXPORT_API CheckPreErrors(){
    if(PreErrors == 3){
        Debug(compilerErrorText);
    }
    return PreErrors;
}

extern "C" void EXPORT_API AllocateClientMem (int flags,int memId){
    cl_mem_flags f;
    switch(flags){
        case 0 : f = CL_MEM_READ_WRITE | CL_MEM_COPY_HOST_PTR;
            break;
        case 1 : f = CL_MEM_READ_ONLY | CL_MEM_COPY_HOST_PTR;
            break;
        case 2 : f = CL_MEM_WRITE_ONLY | CL_MEM_COPY_HOST_PTR;
            break;
        default : f = CL_MEM_READ_WRITE | CL_MEM_COPY_HOST_PTR;
    }
    clMemory[memId].clientMemPtr = clCreateBuffer (context,
                                                   f,
                                                   clMemory[memId].size*clMemory[memId].dsize,
                                                   clMemory[memId].memPtr, &error);
    CheckError(error,  __LINE__);
}


extern "C" int EXPORT_API CreateFrameBuffer(int width,int height){
    fbMemory[fbMemoryIdx] = *new FrameBuffer();
    //generate the texture ID
    glGenTextures(1, &fbMemory[fbMemoryIdx].glTextureID);
    //binnding the texture
    glBindTexture(GL_TEXTURE_2D, fbMemory[fbMemoryIdx].glTextureID);
    //regular sampler params
    glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_S, GL_CLAMP_TO_EDGE);
    glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_T, GL_CLAMP_TO_EDGE);
    //need to set GL_NEAREST
    //(not GL_NEAREST_MIPMAP_* which would cause CL_INVALID_GL_OBJECT later)
    glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MIN_FILTER, GL_NEAREST);
    glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MAG_FILTER, GL_NEAREST);
    //specify texture dimensions, format etc
    glTexImage2D(GL_TEXTURE_2D, 0, GL_RGBA8, width, height, 0, GL_RGBA, GL_FLOAT, 0);
    
    fbMemory[fbMemoryIdx].clTextureMem = clCreateFromGLTexture(context, CL_MEM_WRITE_ONLY, GL_TEXTURE_2D, 0,fbMemory[fbMemoryIdx].glTextureID,NULL);
    fbMemoryIdx ++;
    CheckError(error,  __LINE__);
    return fbMemoryIdx-1;
}

/*char* UStringToChar(char* string,int l){
    char* ret = new char[l+1];
    for(int i = 0; i < l; i++){
        *(ret+i) = *(string+(i*2));
    }
    ret[l] = NULL;
    return ret;
}*/

extern "C" int EXPORT_API CreateKernel (const char* name, int length){
    kernels[kernelsIdx] = clCreateKernel(activeProgram, /*UStringToChar(name,length)*/name, &error);
    kernelsIdx ++;
    clFinish(cq);
    CheckError(error,  __LINE__);
    return (int)kernelsIdx-1;
}

extern "C" void EXPORT_API DispatchKernel (int kernelId, int dimensions,const int* globalWorkSize,const int* localWorkSize, bool lock){
    clFinish(cq);
    
    if(lock){
        glFinish();
        for(int l = 0; l < fbMemoryIdx;l++){
            clEnqueueAcquireGLObjects(cq, 1,  &fbMemory[l].clTextureMem, 0, 0, NULL);
        }
    }
    
    /*Tell the Device, through the command queue, to execute que Kernel */
    size_t* gws = new size_t[dimensions];
    size_t* lws = new size_t[dimensions];
    for(int d = 0; d < dimensions;d++){
        gws[d] = globalWorkSize[d];
        if(localWorkSize != NULL){
            lws[d] = localWorkSize[d];
        }
    }
    if(localWorkSize != NULL){
        error=clEnqueueNDRangeKernel(cq, kernels[kernelId], dimensions, NULL, gws, lws, 0, NULL, NULL);
    }else{
        error=clEnqueueNDRangeKernel(cq, kernels[kernelId], dimensions, NULL, gws, NULL, 0, NULL, NULL);
    }
    CheckError(error,  __LINE__);
    clFinish(cq);
    
    
    if(lock){
        for(int l = 0; l < fbMemoryIdx;l++){
            clEnqueueReleaseGLObjects(cq, 1,  &fbMemory[l].clTextureMem, 0, 0, NULL);
        }
    }
}

extern "C" void EXPORT_API SetKernelArgValue (int kernelId,int argIdx,int data,size_t s){
    error = clSetKernelArg(kernels[kernelId], argIdx, s, &data);
    CheckError(error,  __LINE__);
}

extern "C" void EXPORT_API SetKernelArgLocalMem (int kernelId,int argIdx,size_t size){
    error = clSetKernelArg(kernels[kernelId], argIdx, size, NULL);
    CheckError(error,  __LINE__);
}

extern "C" void EXPORT_API SetKernelArgMem (int kernelId,int argIdx,int memId){
    error = clSetKernelArg(kernels[kernelId], argIdx, sizeof(clMemory[memId].clientMemPtr), &clMemory[memId].clientMemPtr);
    CheckError(error,  __LINE__);
}

extern "C" void EXPORT_API SetKernelArgFB (int kernelId,int argIdx,int memId){
    error = clSetKernelArg(kernels[kernelId], argIdx, sizeof(fbMemory[memId].clTextureMem), &fbMemory[memId].clTextureMem);
    CheckError(error,  memId);
}

extern "C" void EXPORT_API MemCpy_HostToClient (int memId){
    /* Send input data to OpenCL (async, don't alter the buffer!) */
    error=clEnqueueWriteBuffer(cq, clMemory[memId].clientMemPtr, CL_TRUE, 0, clMemory[memId].dsize*clMemory[memId].size, clMemory[memId].memPtr, 0, NULL, NULL);//TODO:int hardcoded
    CheckError(error,  __LINE__);
}

extern "C" void EXPORT_API MemCpy_ClientToHost (int memId){
    error=clEnqueueReadBuffer(cq, clMemory[memId].clientMemPtr, CL_TRUE, 0, clMemory[memId].dsize*clMemory[memId].size, clMemory[memId].memPtr, 0, NULL, NULL);//TODO:Hardcoded int
    CheckError(error,  __LINE__);
}

std::string GetPlatformName (cl_platform_id id)
{
    size_t size = 0;
    clGetPlatformInfo (id, CL_PLATFORM_NAME, 0, nullptr, &size);
    
    std::string result;
    result.resize (size);
    clGetPlatformInfo (id, CL_PLATFORM_NAME, size,
                       const_cast<char*> (result.data ()), nullptr);
    
    return result;
}

std::string GetDeviceName (cl_device_id id)
{
    size_t size = 0;
    clGetDeviceInfo (id, CL_DEVICE_NAME, 0, nullptr, &size);
    
    std::string result;
    result.resize (size);
    clGetDeviceInfo (id, CL_DEVICE_NAME, size,
                     const_cast<char*> (result.data ()), nullptr);
    
    return result;
}

char* FillKernel(char* src){
    std::string srcString = *new std::string(src);
    for(int i = 0; i < srcString.length(); i++){
        if(srcString[i] == '<'&&srcString[i+1] == 'i'&&srcString[i+4] == '>'){
            std::string ins = "";
            switch(srcString[i+2]){
                case 'p' :
                for(int j = 0; j < 119; j++){//TODO:Variable size?
                    ins += "__global int *chunk"+std::to_string(j)+",";
                }
                ins += "__global int *chunk"+std::to_string(119);
                srcString.replace(i,5,ins);
                break;
            }
        }
    }
    return (char*)srcString.c_str();
}

extern "C" int EXPORT_API UnityCreateCLContext (){
    
#if defined(_WIN32)
    
    // Windows
    cl_context_properties properties[] = {
        CL_GL_CONTEXT_KHR, (cl_context_properties)wglGetCurrentContext(),
        CL_WGL_HDC_KHR, (cl_context_properties)wglGetCurrentDC(),
        CL_CONTEXT_PLATFORM, (cl_context_properties)platform,
        0
    };
    
#elif defined(__APPLE__)
    
    // OS X
    CGLContextObj     kCGLContext     = CGLGetCurrentContext();
    CGLShareGroupObj  kCGLShareGroup  = CGLGetShareGroup(kCGLContext);
    
    cl_context_properties contextProperties[] = {
        CL_CONTEXT_PROPERTY_USE_CGL_SHAREGROUP_APPLE,
        (cl_context_properties) kCGLShareGroup,
        0
    };
    
#else
    
    // Linux
    cl_context_properties properties[] = {
        CL_GL_CONTEXT_KHR, (cl_context_properties)glXGetCurrentContext(),
        CL_GLX_DISPLAY_KHR, (cl_context_properties)glXGetCurrentDisplay(),
        CL_CONTEXT_PLATFORM, (cl_context_properties)platform,
        0
    };
    
#endif
    
    
    
    // http://www.khronos.org/registry/cl/sdk/1.1/docs/man/xhtml/clGetPlatformIDs.html
    cl_uint platformIdCount = 0;
    clGetPlatformIDs (0, nullptr, &platformIdCount);
     
    if (platformIdCount == 0) {
        std::cerr << "No OpenCL platform found" << std::endl;
        return 0;
    } else {
        std::cout << "Found " << platformIdCount << " platform(s)" << std::endl;
    }
    
    std::vector<cl_platform_id> platformIds (platformIdCount);
    clGetPlatformIDs (platformIdCount, platformIds.data (), nullptr);
    
    for (cl_uint i = 0; i < platformIdCount; ++i) {
        std::cout << "\t (" << (i+1) << ") : " << GetPlatformName (platformIds [i]) << std::endl;
    }
    
    // http://www.khronos.org/registry/cl/sdk/1.1/docs/man/xhtml/clGetDeviceIDs.html
    cl_uint deviceIdCount = 0;
    clGetDeviceIDs (platformIds [0], CL_DEVICE_TYPE_ALL, 0, nullptr,
                    &deviceIdCount);
    
    if (deviceIdCount == 0) {
        std::cerr << "No OpenCL devices found" << std::endl;
        return 1;
    } else {
        std::cout << "Found " << deviceIdCount << " device(s)" << std::endl;
        //return deviceIdCount;
    }
    
    std::vector<cl_device_id> deviceIds (deviceIdCount);
    clGetDeviceIDs (platformIds [0], CL_DEVICE_TYPE_ALL, deviceIdCount,
                    deviceIds.data (), nullptr);
    
    for (cl_uint i = 0; i < deviceIdCount; ++i) {
        std::cout << "\t (" << (i+1) << ") : " << GetDeviceName (deviceIds [i]) << std::endl;
    }
    
    // http://www.khronos.org/registry/cl/sdk/1.1/docs/man/xhtml/clCreateContext.html
    /*const cl_context_properties contextProperties [] =
    {
        CL_CONTEXT_PLATFORM, reinterpret_cast<cl_context_properties> (platformIds [0]),
        0, 0
    };*/
    num_devices = deviceIdCount;
    deviceIdList = deviceIds.data ();
    context = clCreateContext (contextProperties, num_devices,
                                          deviceIdList, nullptr, nullptr, &error);
    
    if(error  != CL_SUCCESS){
        return 2;
    }
    
    std::cout << "Context created" << std::endl;
    
   
    if(FILE *fil=fopen("/Users/sebastian/UnityProjects/dual/Assets/Kernels/main.cl","r")){//TODO:Hardcode
        
        fseek (fil , 0 , SEEK_END);
        size_t lSize = ftell (fil);
        rewind (fil);
        
       // char src[lSize];
        char *source_str = (char*)malloc(lSize);
        size_t source_size = fread( source_str, 1, lSize, fil);
        fclose( fil );
        //source_str = FillKernel(source_str);
        //source_size = strlen(source_str);
        activeProgram =  clCreateProgramWithSource(context, 1, (const char **)&source_str, (const size_t *)&source_size, &error);
        //free(source_str);
        //size_t srcsize=fread(src, sizeof src, 1, fil);
        //fclose(fil);
        //src[srcsize+1] = '\0';
        //const char *srcptr[]={src};
        /* Submit the source code of the kernel to OpenCL, and create a program object with it */
        //activeProgram =  clCreateProgramWithSource(context,
        //                                           1, srcptr, &srcsize, &error);
        error = clBuildProgram(activeProgram, 0, NULL, "", NULL, NULL);
        //delete[] source_str;
        //TODO:Create Compile Errorlog
        //cl_kernel k_example=clCreateKernel(activeProgram, "SAXPY", &error);
        if ( error != CL_SUCCESS ) {
            std::string s = "Error on buildProgram";
            s += "\n Error number "+std::to_string(error);
            s += "\nRequestingInfo\n";
            //sprintf(compilerErrorText,"\n Error number %d", error);
            //sprintf( compilerErrorText, "\nRequestingInfo\n" );
            compilerErrorText = new char[4096+strlen(s.c_str())];
            char build_c[4096];
            clGetProgramBuildInfo( activeProgram, *(deviceIdList+1), CL_PROGRAM_BUILD_LOG, 4096, build_c, NULL );

            strncpy(compilerErrorText,s.c_str(),strlen(s.c_str()));
            strncpy(compilerErrorText+strlen(s.c_str()),build_c,4096);

            //sprintf(compilerErrorText, "Build Log for %s_program:\n%s\n", "main", build_c );
            return 3;
        }
    }else{
        return 4;
    }
    cq = clCreateCommandQueue(context, *(deviceIdList+1), 0, &error);
    if (error != CL_SUCCESS) {
        return 5;
    }
    
    //Log max work group size
   /* size_t* buf_uint = new size_t[3];
    buf_uint[0] = 0;
    buf_uint[1] = 0;
    buf_uint[2] = 0;
    clGetDeviceInfo(*deviceIdList,CL_DEVICE_MAX_WORK_ITEM_DIMENSIONS,sizeof(size_t)*3,&buf_uint,NULL);
    return static_cast<int>(buf_uint[0]);*/
    
    /*cl_ulong workitem_size = (cl_ulong) 0;
    clGetDeviceInfo(*(deviceIdList+1),CL_DEVICE_GLOBAL_MEM_SIZE, sizeof(workitem_size), &workitem_size, NULL);
    return workitem_size>20000000?1000:0;*/
    //printf("CL_DEVICE_MAX_WORK_ITEM_SIZES:\t%u / %u / %u \n", workitem_size[0], workitem_size[1], workitem_size[2]);
    return -1;
}


// --------------------------------------------------------------------------
// UnityRenderEvent
// This will be called for GL.IssuePluginEvent script calls; eventID will
// be the integer passed to IssuePluginEvent. In this example, we just ignore
// that value.


// --------------------------------------------------------------------------
// UnitySetGraphicsDevice

static int g_DeviceType = -1;

extern "C" void EXPORT_API Reset (){
    /*delete(clMemory);TODO:Dealloc?
    clMemoryIdx = 0;
    delete(kernels);
    kernelsIdx = 0;*/
}

extern "C" void EXPORT_API UnitySetGraphicsDevice (void* device, int deviceType, int eventType)
{
    // Set device type to -1, i.e. "not recognized by our plugin"
    g_DeviceType = -1;
    
#if SUPPORT_OPENGL
    // If we've got an OpenGL device, remember device type. There's no OpenGL
    // "device pointer" to remember since OpenGL always operates on a currently set
    // global context.
    /*if (deviceType == kGfxRendererOpenGL)
    {
        //DebugLog ("Set OpenGL graphics device\n");
        g_DeviceType = deviceType;
    }*/
#endif
    PreErrors = UnityCreateCLContext();
}

