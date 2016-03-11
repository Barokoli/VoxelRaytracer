using UnityEngine;
using System.Collections;
using System.Runtime.InteropServices;
using System.IO;
using System;


public unsafe class CL_Handler : MonoBehaviour {

	string[] OutList = {
		"No OpenCL platform found",
		"No OpenCL devices found",
		"Context creation failed",
		"Invalid program (compiler err)",
		"Kernel File not found",
		"command Queue init failed."
	};

	int activeKernelId;

	//public static Texture2D debugTex;

	public static float* RandomNumbers;
	public static int RandomNumberMemId;

	public static uint* tmpChunkData;
	public static int tmpChunkDataId;
	public static uint* tmpScanData;//TODO:remove
	public static int tmpScanDataId;
	public static uint* tmpScanLengthData;//TODO:remove public static
	public static int tmpScanLengthDataId;
	public static int tmpDataSize = 0;
	public static int tmpDataSizeCube = 0;

	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void DebugDelegate(string str);

	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void FreeAllocatedMemory(void* data);

	[DllImport ("OpenGLPlugin")]
	public static extern void SetDebugFunction( IntPtr fp );

	[DllImport ("OpenGLPlugin")]
	public static extern int CheckPreErrors();

	[DllImport("OpenGLPlugin")]
	private static extern IntPtr GetRenderEventFunc();

	[DllImport ("OpenGLPlugin")]
	public static extern int AllocateHostMem(void* data,int size, uint dsize);  //Allocate Memory on Host (Data,Data Length,Format)

	[DllImport ("OpenGLPlugin")]
	public static extern int CreateFrameBuffer(int width,int height);  //OpenGL Framebuffer

	[DllImport ("OpenGLPlugin")]
	public static extern int AllocateHostMem(int data,int size, uint dsize); //Allocate Int on Host Memory

	[DllImport ("OpenGLPlugin")]
	public static extern void AllocateClientMem(int flags, int memId);  //Allocate GPU representative from Host Memory

	[DllImport ("OpenGLPlugin")]
	public static extern int CreateKernel(string name);  //create Kernel

	[DllImport ("OpenGLPlugin")]
	public static extern void DispatchKernel(int kernelId, int dimensions,int* globalWorkSize,int* localWorkSize,bool l);  //run a Kernel

	[DllImport ("OpenGLPlugin")]
	public static extern void SetKernelArgMem(int kernelId,int argIdx,int memId);  //Set Kernel Memory

	[DllImport ("OpenGLPlugin")]
	public static extern void SetKernelArgLocalMem(int kernelId,int argIdx,uint size);  //Set shared Memory size

	[DllImport ("OpenGLPlugin")]
	public static extern void SetKernelArgValue(int kernelId,int argIdx,int value,uint dsize);  //Set a simple Value

	[DllImport ("OpenGLPlugin")]
	public static extern void SetKernelArgFB(int kernelId,int argIdx,int memId);  //Set Framebuffer to kernel

	[DllImport ("OpenGLPlugin")]
	public static extern void MemCpy_HostToClient (int memId);  //Copy from Host to Client

	[DllImport ("OpenGLPlugin")]
	public static extern void MemCpy_ClientToHost (int memId);  //Copy from Client to Host

	[DllImport ("OpenGLPlugin")]
	public static extern void Reset();

	void Start () {

		//Debuging code
		//debugTex = new Texture2D(Screen.width,Screen.height);

		DebugDelegate callback_delegate = new DebugDelegate( CallBackLog );
		// Convert callback_delegate into a function pointer that can be
		// used in unmanaged code.
		IntPtr intptr_delegate = Marshal.GetFunctionPointerForDelegate(callback_delegate);
		// Call the API passing along the function pointer.
		SetDebugFunction( intptr_delegate );

		if(PreErrCheck(CheckPreErrors())){  //Initialize OpenCL context etc

			activeKernelId = createKernel("random_number_kernel");

			RandomNumbers = (float*) Unmanaged.New<float>(Terrain_Handler.NoiseSize3*Terrain_Handler.NoiseCount);
			RandomNumberMemId = AllocateHostMem(RandomNumbers,Terrain_Handler.NoiseSize3*Terrain_Handler.NoiseCount,sizeof(float));
			AllocateClientMem(0,RandomNumberMemId);
			//MemCpy_HostToClient(RandomNumberMemId);
			SetKernelArgMem(activeKernelId,0,RandomNumberMemId);
			SetKernelArgValue(activeKernelId,1,Terrain_Handler.seed,sizeof(int));
			int* globalWorkSize = (int*) Unmanaged.New<int>(3);
			int* localWorkSize = (int*) Unmanaged.New<int>(3);
			globalWorkSize[0] = (int)Terrain_Handler.NoiseSize*2;
			globalWorkSize[1] = (int)Terrain_Handler.NoiseSize*2;
			globalWorkSize[2] = (int)Terrain_Handler.NoiseSize*2;
			DispatchKernel(activeKernelId,3,globalWorkSize,null,false);  //Create random Numbers
			//MemCpy_ClientToHost(RandomNumberMemId);//TODO:Leave on client?
			//Debug.Log(RandomNumbers[0]);
			Unmanaged.Free(globalWorkSize);
			Unmanaged.Free(localWorkSize);
			int cSize = (int)(Terrain_Handler.ChunkSize.x*Terrain_Handler.ChunkSize.y*Terrain_Handler.ChunkSize.z);
			int i = cSize;
			while(i > 0){
				tmpDataSize += i;
				i = i>>3;
			}

			tmpDataSizeCube = ToCube(tmpDataSize);
			Debug.Log("tmpDataSize: "+tmpDataSize);
			tmpChunkData = (uint*) Unmanaged.New<uint>(tmpDataSizeCube);
			tmpChunkDataId = AllocateHostMem(tmpChunkData,tmpDataSizeCube,sizeof(int));
			AllocateClientMem(0,tmpChunkDataId);
			Debug.Log("Cube size: "+tmpDataSizeCube);

			tmpScanData = (uint*) Unmanaged.New<uint>(tmpDataSizeCube);
			tmpScanDataId = AllocateHostMem(tmpScanData,tmpDataSizeCube,sizeof(int));
			AllocateClientMem(0,tmpScanDataId);

			tmpScanLengthData = (uint*) Unmanaged.New<uint>(tmpDataSizeCube/Terrain_Handler.MaxChunkSize);
			tmpScanLengthDataId = AllocateHostMem(tmpScanLengthData,tmpDataSizeCube/Terrain_Handler.MaxChunkSize,sizeof(int));//TODO:Hardcoded Max Work item size
			AllocateClientMem(0,tmpScanLengthDataId);

			StartCoroutine("CallPluginAtEndOfFrames");//Start Renderer
		}
	}

	private IEnumerator CallPluginAtEndOfFrames()
	{
		while (true) {
			// Wait until all frame rendering is done
			yield return new WaitForEndOfFrame();

			// Issue a plugin event with arbitrary integer identifier.
			GL.IssuePluginEvent(GetRenderEventFunc(), 1);
		}
	}

	void OnGUI(){
		/*GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), debugTex, ScaleMode.StretchToFill, true, 0);
		if (Input.GetMouseButtonDown(0)){
			Debug.Log(debugTex.GetPixel((int)Input.mousePosition.x,(int)Input.mousePosition.y));
		}*/
	}

	int ToCube (int n){
		return (int)Mathf.Ceil((float)n/(float)Terrain_Handler.MaxChunkSize)*Terrain_Handler.MaxChunkSize;
	}

	void OnApplicationQuit() {
		Reset();
		Unmanaged.Free(RandomNumbers);
		Unmanaged.Free(tmpChunkData);
		Unmanaged.Free(tmpScanData);
		Unmanaged.Free(tmpScanLengthData);
	}

	/*static void FreeAllocMem(void* data){
		Unmanaged.Free(data);
	}*/

	static void CallBackLog(string str){
		Debug.Log(":Native Log: " + str);
	}
	// Update is called once per frame
	void Update () {
		if(Input.GetKeyDown("space")){
			Debug.Log(tmpChunkData[2]);
		}
	}

	bool PreErrCheck(int i){
		if(i == -1){
			Debug.Log("Successful context and program initialization.");
			return true;
		}else{
			if(i>OutList.Length){
				Debug.Log("errorlognr: "+i);
			}else{
				Debug.Log(OutList[i]);
			}
			return false;
		}
	}

	public static int createKernel(string s){
		int ri = CreateKernel(s);
		return ri;
	}

}
