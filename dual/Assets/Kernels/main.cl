#define UseAverage 1

#if UseAverage
static int average(int a,int b,int c,int d,int e,int f,int g,int h){
    return floor(((0x000000FF&a)+(0x000000FF&a)+(0x000000FF&a)+(0x000000FF&a)+(0x000000FF&a)+(0x000000FF&a)+(0x000000FF&a)+(0x000000FF&a))*0.125f);
}
#endif

static int rand(int* seed) // 1 <= *seed < m
{
    int const a = 16807; //ie 7**5
    int const m = 2147483647; //ie 2**31-1

    *seed = (long)(*seed * a)%m;
    return(*seed);
}

//Receive Values for Blocks from Random Memory
static int GetBlock(int x, int y, int z,__global float* r, int noiseCount, int noiseSize){
    //return x%2;
    //return x;
    //return y*2.0f;
    //return (int)(y*2.0f);
    //return (x==35 && y == 12 && z == 4)?255:0;//x+y*128+z*128*128;//(y%128);//>50?0:128;//(y%128);//((x%128)+(y%128)*128+(z%128)*128*128)%2;
    //return (int)((r[z%128]+1.0f)*127.5f);
    int xf,yf,zf,xpf,ypf,zpf,nSsqr;
    nSsqr = noiseSize*noiseSize;
    float xv,yv,zv;
    xv = (float)x/((float)noiseSize);//*7.654321f);
    yv = (float)y/((float)noiseSize);//*7.654321f);
    zv = (float)z/((float)noiseSize);//*7.654321f);
    xf = floor(xv);
    yf = floor(yv);
    zf = floor(zv);
    xv -= xf;
    yv -= yf;
    zv -= zf;
    xf = xv*(float)noiseSize;
    yf = yv*(float)noiseSize;
    zf = zv*(float)noiseSize;
    xpf = xf<(noiseSize-1)?xf+1:0;
    ypf = yf<(noiseSize-1)?yf+1:0;
    zpf = zf<(noiseSize-1)?zf+1:0;
    float a,b,c,d,e,f,g,h;
    a = r[xf +yf *noiseSize+zf *nSsqr];
    b = r[xpf+yf *noiseSize+zf *nSsqr];
    c = r[xf +ypf*noiseSize+zf *nSsqr];
    d = r[xpf+ypf*noiseSize+zf *nSsqr];
    e = r[xf +yf *noiseSize+zpf*nSsqr];
    f = r[xpf+yf *noiseSize+zpf*nSsqr];
    g = r[xf +ypf*noiseSize+zpf*nSsqr];
    h = r[xpf+ypf*noiseSize+zpf*nSsqr];
    float value = zv*(a*xv+b*(1.0f-xv))+(1.0f-zv)*(c*yv+d*(1.0f-yv));
    value = ((float)((float)(127-y)/64.0f)-1.0f)+value*0.07;
    //value = ((float)((float)y/64.0f)-1.0f);
    //Clamp
    value = value > 0.1f?1.0f:value;
    value = value < -0.1f?-1.0f:value;
    //endClamp
    return (int)(127.5f*(value+1.0f));//255;//(yf%2)==1?255:0;//(int)yf;//(int)(127.5f*(value+1.0f));//TODO: if Clamp is used resolution can be improved. currently uses one 10th of possible res.
}

//Fill Memory with random Values from seed
__kernel void random_number_kernel(__global float* seed_memory, int start_seed)
{
    int global_id = get_global_id(0)+get_global_id(1)*get_global_size(0)+get_global_id(2)*get_global_size(0)*get_global_size(1); // Get the global id in 3D.

    int n = global_id + start_seed;

    n = (n<<13) ^ n;

    seed_memory[global_id] = (1.0f - ( (n * (n * n * 15731 + 789221) +

                                        1376312589)&0x7fffffff)* 0.000000000931322574615478515625f);
}


//0xC0000000 Data leaf
//0x80000000 Data node
//TODO: shared memory?
//TODO: scan first stages in own Memory?
/*
 Memory Block for every Octree Level
 0xFF000000 <- Octree info
 0x00FFFFFF <- Block info
    0x000000FF <- Density info
 0xC0 Data leaf
 0x80 Data node
 0x00 Empty node
 */
__kernel void chunkInit1(__global int* tmpData,__global float* r, int noiseCount, int noiseSize, int xOff, int yOff, int zOff){//init with 8th of kernels
    int global_id = get_global_id(0)+get_global_id(1)*get_global_size(0)+get_global_id(2)*get_global_size(0)*get_global_size(1); // Get the global id in 3D. 8th
    
    xOff = 0;
    yOff = 0;
    zOff = 0;
    
    int x = get_global_id(0);
    int y = get_global_id(1);
    int z = get_global_id(2);
    int size = get_global_size(0)*get_global_size(1)*get_global_size(2);

    int a,b,c,d,e,f,g,h;
    
    //id = x*2+y*get_global_size(0)*4+z*get_global_size(0)*get_global_size(1)*8;//global_id*8;
    int id = (x+y*get_global_size(0)+z*get_global_size(0)*get_global_size(1))*8;
    //int globId = x+y*get_global_size(0)+z*get_global_size(0)*get_global_size(1);
    int globId = ((((int)(x*0.5f)) + (int)((int)y*0.5f)*get_global_size(0)*0.5f + (int)((int)z*0.5f)*get_global_size(0)*get_global_size(1)*0.25f))*8 + (x%2+((y%2)*2)+((z%2)*4));

    a = GetBlock(x*2+xOff  ,y*2+yOff  ,z*2+zOff  ,r,noiseCount,noiseSize);
    b = GetBlock(x*2+xOff+1,y*2+yOff  ,z*2+zOff  ,r,noiseCount,noiseSize);
    c = GetBlock(x*2+xOff  ,y*2+yOff+1,z*2+zOff  ,r,noiseCount,noiseSize);
    d = GetBlock(x*2+xOff+1,y*2+yOff+1,z*2+zOff  ,r,noiseCount,noiseSize);
    e = GetBlock(x*2+xOff  ,y*2+yOff  ,z*2+zOff+1,r,noiseCount,noiseSize);
    f = GetBlock(x*2+xOff+1,y*2+yOff  ,z*2+zOff+1,r,noiseCount,noiseSize);
    g = GetBlock(x*2+xOff  ,y*2+yOff+1,z*2+zOff+1,r,noiseCount,noiseSize);
    h = GetBlock(x*2+xOff+1,y*2+yOff+1,z*2+zOff+1,r,noiseCount,noiseSize);

    if(a == b &&a == c &&a == d &&a == e &&a == f &&a == g &&a == h){
        tmpData[id  ] =(int) 0x00000000;
        tmpData[id+1] =(int) 0x00000000;
        tmpData[id+2] =(int) 0x00000000;
        tmpData[id+3] =(int) 0x00000000;
        tmpData[id+4] =(int) 0x00000000;
        tmpData[id+5] =(int) 0x00000000;
        tmpData[id+6] =(int) 0x00000000;
        tmpData[id+7] =(int) 0x00000000;
        tmpData[(size<<3)+ globId] = (int) (0xC0000000)|(0x00FFFFFF&a);
        /*tmpData[(size<<3)+
                (int)(x/2.0f)*8+x%2+
                ((int)(y/2.0f)*8)*(get_global_size(0)>>1)+y%2+
                ((int)(z/2.0f)*8)*(get_global_size(0)*get_global_size(1)>>2)+z%2
                ] = (int) (0xC0000000)|(0x00FFFFFF&a);//TODO:Might fail*/
    }else{
        tmpData[id  ] =(int) (0xC0000000)|(0x00FFFFFF&a);
        tmpData[id+1] =(int) (0xC0000000)|(0x00FFFFFF&b);
        tmpData[id+2] =(int) (0xC0000000)|(0x00FFFFFF&c);
        tmpData[id+3] =(int) (0xC0000000)|(0x00FFFFFF&d);
        tmpData[id+4] =(int) (0xC0000000)|(0x00FFFFFF&e);
        tmpData[id+5] =(int) (0xC0000000)|(0x00FFFFFF&f);
        tmpData[id+6] =(int) (0xC0000000)|(0x00FFFFFF&g);
        tmpData[id+7] =(int) (0xC0000000)|(0x00FFFFFF&h);
/*#if UseAverage
        tmpData[(size<<3)+global_id] =(int) (0x80000000)|(0x00FFFFFF&(average(a,b,c,d,e,f,g,h)));
#else
        tmpData[(size<<3)+global_id] =(int) (0x80000000)|(0x00FFFFFF&a);//TODO: instead of "a" average of "a->h"?
#endif*/
        tmpData[(size<<3)+ globId] =(int) (0x80000000)|(0x00FFFFFF&id);
    }
}

__kernel void chunkInit2(__global int* tmpData, int Off){//init with 8th of kernels
    int global_id = get_global_id(0)+get_global_id(1)*get_global_size(0)+get_global_id(2)*get_global_size(0)*get_global_size(1); // 8th
    int a,b,c,d,e,f,g,h;

    int x = get_global_id(0);
    int y = get_global_id(1);
    int z = get_global_id(2);
    
    int id = (x+y*get_global_size(0)+z*get_global_size(0)*get_global_size(1))*8;
    int globId = ((((int)(x*0.5f)) + (int)((int)y*0.5f)*get_global_size(0)*0.5f + (int)((int)z*0.5f)*get_global_size(0)*get_global_size(1)*0.25f))*8 + (x%2+((y%2)*2)+((z%2)*4));
    
    int cubeSize = get_global_size(0)*get_global_size(1)*get_global_size(2);
    

    a = tmpData[Off+id  ];
    b = tmpData[Off+id+1];
    c = tmpData[Off+id+2];
    d = tmpData[Off+id+3];
    e = tmpData[Off+id+4];
    f = tmpData[Off+id+5];
    g = tmpData[Off+id+6];
    h = tmpData[Off+id+7];

    if(a == b &&a == c &&a == d &&a == e &&a == f &&a == g &&a == h&&(a&0x40000000)!=0){
        tmpData[Off+id  ] = (int) 0x00000000;
        tmpData[Off+id+1] = (int) 0x00000000;
        tmpData[Off+id+2] = (int) 0x00000000;
        tmpData[Off+id+3] = (int) 0x00000000;
        tmpData[Off+id+4] = (int) 0x00000000;
        tmpData[Off+id+5] = (int) 0x00000000;
        tmpData[Off+id+6] = (int) 0x00000000;
        tmpData[Off+id+7] = (int) 0x00000000;
        
        tmpData[Off+(cubeSize<<3)+globId] = (int) (0xC0000000)|(0x00FFFFFF&a);
    }else{
        /*#if UseAverage
        tmpData[Off+(get_global_size(0)<<3)+global_id] =(int) (0x80000000)|(0x00FFFFFF&(average(a,b,c,d,e,f,g,h)));
        #else
        tmpData[Off+(get_global_size(0)<<3)+global_id] =(int) (0x80000000)|(0x00FFFFFF&a);//TODO: instead of "a" average of "a->h"?
        #endif*/
        
        tmpData[Off+(cubeSize<<3)+globId] = (int) (0x80000000)|(0x00FFFFFF&(Off+id));
    }
}

//inspired by http://www.nehalemlabs.net/prototype/blog/2014/06/23/parallel-programming-with-opencl-and-python-parallel-scan/
__kernel void BScan(__global uint *a,
                    __global uint *r,
                    __local uint *b,
                    uint n_items,
                    uint nullMem,
                    __global uint *length)
{
    uint gid = get_global_id(0);
    uint lid = get_local_id(0);
    uint dp = 1;

    if(gid*2 >= nullMem){
        a[2*gid] = 0;
        a[2*gid+1] = 0;
    }
    b[2*lid] = (a[2*gid]&0xFF000000)!=0?1:0;
    b[2*lid+1] = (a[2*gid+1]&0xFF000000)!=0?1:0;

    for(uint s = n_items>>1; s > 0; s >>= 1) {
        barrier(CLK_LOCAL_MEM_FENCE);
        if(lid < s) {
            uint i = dp*(2*lid+1)-1;
            uint j = dp*(2*lid+2)-1;
            b[j] += b[i];
        }

        dp <<= 1;
    }
    if(lid == 0) {
        length[get_group_id(0)] = b[n_items - 1];
        b[n_items - 1] = 0;
    }

    for(uint s = 1; s < n_items; s <<= 1) {
        dp >>= 1;
        barrier(CLK_LOCAL_MEM_FENCE);

        if(lid < s) {
            uint i = dp*(2*lid+1)-1;
            uint j = dp*(2*lid+2)-1;

            uint t = b[j];
            b[j] += b[i];
            b[i] = t;
        }
    }

    barrier(CLK_LOCAL_MEM_FENCE);

    r[2*gid] = b[2*lid];
    r[2*gid+1] = b[2*lid+1];
}

//Copy Data from Scanned unsorted to a sorted Memory
__kernel void chunkMemCpy(__global uint *chunk,__global uint *scan,__global uint *result,__global uint *length,uint dChunkSize){
    int gid = get_global_id(0);
    uint value = chunk[gid];
    if(value!=0){
        if((value&0xFF000000) == 0x80000000){
            if(((gid-gid%dChunkSize)/dChunkSize)-1.0<0){
                value = 0x80000000 | ( (int)scan[value&0x00FFFFFF] );
            }else{
                value = 0x80000000 | ( (int)scan[value&0x00FFFFFF]+(int)length[(((value&0x00FFFFFF)-(value&0x00FFFFFF)%dChunkSize)/dChunkSize)] );
            }
        }
        if(((gid-gid%dChunkSize)/dChunkSize)-1.0<0){
            result[(int)scan[gid]] = value;
        }else{
            result[(int)scan[gid]+(int)length[((gid-gid%dChunkSize)/dChunkSize)]] = value;
        }
    }
}

//Get Value of a Block in Octree
static uint EvaluateBlock(int relX,int relY,int relZ,__global int *chunk,uint lastI){
    float4 bPos = float4(0,0,0,0);
    uint block = chunk[lastI-1];

    uint off = 0;

    uint lvl = 64;//TODO:Hardcoded Max size 128?
    //uint step = 1;

    while((block&0xFF000000) != 0xC0000000){
        off |= (uint)(relX>=(bPos.x+lvl)? 1:0);
        off |= (uint)(relY>=(bPos.y+lvl)? 2:0);
        off |= (uint)(relZ>=(bPos.z+lvl)? 4:0);
        block = chunk[(block&0x00FFFFFF)+off];
        bPos += (float4)((off&1)*lvl,((off&2)>>1)*lvl,((off&4)>>2)*lvl,0.0);
        lvl = lvl >> 1;
        off = 0;
        //step = step << 1;
    }
    //block &= 0x00FFFFFF;
    //block |= lvl<<25;
    return block&0x00FFFFFF;
    //return chunk[(int)(relPos.x*128+relPos.y)]&0x00FFFFFF;
}

__kernel void RenderSlice(__global int *fBuffer,__global int *chunk,int sliceX,int ChunkL,int depth,int width,int height){
    int idx = get_global_id(0);
    int idy = get_global_id(1);
    
    fBuffer[idx+idy*get_global_size(0)] = EvaluateBlock(sliceX,idy,idx,chunk,(uint)(ChunkL));
}

static int castRay(float4 origin, float4 dir,__global int *chunk,int chunkL){
    float depth = 0;
    float rx,ry,rz;//,cx,cy,cz;
    float foo;
    float mag;
    
    while(EvaluateBlock((int)origin.x,(int)origin.y,(int)origin.z,chunk,chunkL)<127){
        /*cx = origin.x>0?((float)fract(origin.x,&foo)):1.0f-((float)fract(origin.x,&foo));
        cy = ((float)fract(origin.y,&foo));
        cz = ((float)fract(origin.z,&foo));*/
        
        rx = dir.x!=0.0f? (float)(
                           dir.x>0.0f?
                           (1.0f-(float)fract(origin.x,&foo)):
                           (float)-fract(origin.x,&foo)
                           ) /dir.x
                    :100.0f;
        ry = dir.y!=0.0f? (float)(
                           dir.y>0.0f?
                           (1.0f-(float)fract(origin.y,&foo)):
                           (float)-fract(origin.y,&foo)
                           ) /dir.y
                    :100.0f;
        rz = dir.z!=0.0f? (float)(
                           dir.z>0.0f?
                           (1.0f-(float)fract(origin.z,&foo)):
                           (float)-fract(origin.z,&foo)
                           ) /dir.z
                    :100.0f;

        /*rx = dir.x!=0.0f? (float)(
                                  dir.x>0.0f?
                                  (1.0f-(float)fract(origin.x,&foo)):
                                  (float)-fract(origin.x,&foo)
                                  ) /dir.x
        :100.0f;
        ry = dir.y!=0.0f? (float)(
                                  dir.y>0.0f?
                                  (1.0f-(float)fract(origin.y,&foo)):
                                  (float)-fract(origin.y,&foo)
                                  ) /dir.y
        :100.0f;
        rz = dir.z!=0.0f? (float)(
                                  dir.z>0.0f?
                                  (1.0f-(float)fract(origin.z,&foo)):
                                  (float)-fract(origin.z,&foo)
                                  ) /dir.z
        :100.0f;*/
        
        /*rx = dir.x!=0.0f?(1.0f-(float)fract(origin.x,&foo))/dir.x:100.0f;
        ry = dir.y!=0.0f?(1.0f-(float)fract(origin.y,&foo))/dir.y:100.0f;
        rz = dir.z!=0.0f?(1.0f-(float)fract(origin.z,&foo))/dir.z:100.0f;*/
        
        rx = rx>0?rx:10000.0f;
        ry = ry>0?ry:10000.0f;
        rz = rz>0?rz:10000.0f;
        
        mag = (float)minmag(minmag(rx,ry),rz);
        origin = origin+(dir*1.01f)*mag;//0.007 große Luecken
        depth += mag;
        if(!(origin.x > 0 && origin.x < 128 &&
             origin.y > 0 && origin.y < 128 &&
             origin.z > 0 && origin.z < 128)){
            return 0;
        }
        /*if(depth == 0){
            return 0;
        }*/
        
        //return 0;
    }
    return depth;
}

static float4 boxRayIntersection(float4 origin,float4 dir, float4 pos){
    //size 128
    float rxp,ryp,rzp,rxn,ryn,rzn;
    rxp = dir.x!=0.0f?((pos.x)-origin.x)/dir.x:1000.0f;
    ryp = dir.y!=0.0f?((pos.y)-origin.y)/dir.y:1000.0f;
    rzp = dir.z!=0.0f?((pos.z)-origin.z)/dir.z:1000.0f;
    
    rxn = dir.x!=0.0f?((pos.x+128)-origin.x)/dir.x:1000.0f;
    ryn = dir.y!=0.0f?((pos.y+128)-origin.y)/dir.y:1000.0f;
    rzn = dir.z!=0.0f?((pos.z+128)-origin.z)/dir.z:1000.0f;
    
    rxp = rxp>0?rxp:1000.0f;
    ryp = ryp>0?ryp:1000.0f;
    rzp = rzp>0?rzp:1000.0f;
    
    rxn = rxn>0?rxn:1000.0f;
    ryn = ryn>0?ryn:1000.0f;
    rzn = rzn>0?rzn:1000.0f;
    
    float4 ipos = origin+dir* minmag(minmag(minmag(rxp,ryp),minmag(rzp,rxn)),minmag(ryn,rzn));
    if(!(ipos.x >= (pos.x) && ipos.x <= (pos.x+128.0f) &&
         ipos.y >= (pos.y) && ipos.y <= (pos.y+128.0f) &&
         ipos.z >= (pos.z) && ipos.z <= (pos.z+128.0f))){
        return (float4)(-1.0f,-1.0f,-1.0f,0.0f);
    }else{
        return ipos;
    }
}

// Frame Buffer, ChunkMemory, ChunkMemory Length, Vector Array, max depth
/** 0-2 Camera Position
 * 3-5 Right Camera Verctor
 * 6-8 Up Camera Vector
 * 9-11 Forward Camera Vector
 * 12-14 Chunk Position
 * */

__kernel void InitRaycaster(write_only image2d_t output,__global int *chunk,int chunkL,__global float *vec,int depth){
    float screenX = get_global_id(0);
    float screenY = get_global_id(1);
    
    write_imagef(output, (int2)(screenX,screenY), (float4)(0.0f,1.0f,0.0f,1.0f));
    
    /*int uvId = get_global_id(0)+get_global_size(0)*(get_global_size(1)-get_global_id(1)-1);
    
    float xlerp = (screenX/get_global_size(0))-0.5f;
    float ylerp = (screenY/get_global_size(0))-0.5f;
    
    float4 camPos = (float4)(vec[0],vec[1],vec[2],0.0f);
    float4 rcv = (float4)(vec[3],vec[4],vec[5],0.0f);
    float4 ucv = (float4)(vec[6],vec[7],vec[8],0.0f);
    float4 fcv = (float4)(vec[9],vec[10],vec[11],0.0f);
    float4 cPos = (float4)(vec[12],vec[13],vec[14],0.0f);
    float4 clipOff = (float4)(vec[15],vec[16],vec[17],0.0f);
    
    float4 origin = clipOff+xlerp*rcv+ylerp*ucv+camPos;
    fcv = origin-camPos;
    float d = sqrt(fcv.x*fcv.x+fcv.y*fcv.y+fcv.z*fcv.z);
    fcv = fcv/d;
    //fcv = new float4(0,0,0);
    
    if(origin.x > cPos.x && origin.x < cPos.x+128 &&
       origin.y > cPos.y && origin.y < cPos.y+128 &&
       origin.z > cPos.z && origin.z < cPos.z+128){
        fBuffer[uvId] = castRay(origin-cPos,fcv,chunk,chunkL);
        //if(EvaluateBlock(origin.x-cPos.x,origin.y-cPos.y,origin.z-cPos.z,chunk,chunkL)>0){
    }else{*/
        /*float4 intersect = boxRayIntersection(origin,fcv,cPos);
        if(intersect.x != -1){
            //fBuffer[uvId] = 255;
            fBuffer[uvId] = castRay(intersect-cPos,fcv,chunk,chunkL);
        }else{*/
   /*         fBuffer[uvId] = 0;
        //}
    }
    //fBuffer[uvId] = 127+xlerp*255;
    //fBuffer[idx+idy*get_global_size(0)] = (int) EvaluateBlock(sliceX,idx,idy,chunk,(uint)(ChunkL-1));
    //fBuffer[idx+idy*get_global_size(0)] =(int) EvaluateBlock(float4(sliceX,idx,idy,0),chunk,(uint)(ChunkL-1));*/
}

__kernel void Raycaster(__global int *fBuffer,__global int *chunk,/*__global float4 *dir,*/__global float4 *pos){
    int idx = get_global_id(0);
    int idy = get_global_id(1);

}

