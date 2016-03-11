using UnityEngine;
using System.Collections;

public unsafe class Renderman {
	public Camera renderCam;
	uint* fBuffer;
	int imageSize;
	Vector2 imageSize2;
	int fBufferId;
	int glFrameBufferId;
	int* renderQueue;
	//RayData
	Color* dir;
	int dirId;
	Color* pos;
	int posId;
	float* initDirection;
	int initDirectionId;

	//int renderQueueId;
	int idx;

	int renderKernel;
	int* globalWorkSize;

	int slicekernel;

	//Debug
	float slice = 0;

	public Renderman(){
		idx = 0;
		renderCam = Camera.main;
		imageSize = Screen.width*Screen.height;//TODO:Resizeable?
		imageSize2 = new Vector2(Screen.width,Screen.height);

		//Initialize unmanaged Memory
		fBuffer = (uint*) Unmanaged.New<uint>((int)imageSize);
		fBufferId = CL_Handler.AllocateHostMem(fBuffer,imageSize,sizeof(int));
		CL_Handler.AllocateClientMem(0,fBufferId);

		initDirection = (float*) Unmanaged.New<float>((int)32);
		initDirectionId = CL_Handler.AllocateHostMem(initDirection,32,sizeof(float));
		CL_Handler.AllocateClientMem(0,initDirectionId);

		//Initialize Kernel
		renderKernel = CL_Handler.createKernel("InitRaycaster");

		slicekernel = CL_Handler.createKernel("RenderSlice");

		//WorkSize
		globalWorkSize = (int*) Unmanaged.New<int>(2);
		globalWorkSize[0] = (int)imageSize2.x;
		globalWorkSize[1] = (int)imageSize2.y;

		glFrameBufferId = CL_Handler.CreateFrameBuffer((int)imageSize2.x,(int)imageSize2.y);
		//renderCam.enabled = false;
	}

	~Renderman(){
		if(fBuffer != null){
			//GameObject.Destroy(host,0.0f);
			Unmanaged.Free(fBuffer);
			Unmanaged.Free(renderQueue);
			Unmanaged.Free(dir);
			Unmanaged.Free(pos);
			Unmanaged.Free(initDirection);
		}
		Unmanaged.Free(globalWorkSize);
	}

	public void renderTestSlice(int ix){
		//slice = slice >= 1f?0.0f:slice+0.01f;
		//Debug.Log((int)Terrain_Handler.Chunks[0].memId);
		//return;
		/*string s = "Chunk Length: "+(int)Terrain_Handler.Chunks[0].MemLength+"\n";
		for(int we = 0; we < (int)Terrain_Handler.Chunks[0].MemLength;we++){
			s += (Terrain_Handler.Chunks[0].chunkMem[we]&0xFF000000)==0xC0000000?"L":"N";
			s += " "+(int)(Terrain_Handler.Chunks[0].chunkMem[we]&0x00FFFFFF)+";";
			if(we%8==7){
				s += "\n";
			}
		}
		Debug.Log(s);
		Debug.Log("Block evaluation: "+evaluateBlock((int)35,(int)12,(int)4,Terrain_Handler.Chunks[0].chunkMem,Terrain_Handler.Chunks[0].MemLength));*/
		CL_Handler.MemCpy_HostToClient((int)Terrain_Handler.Chunks[0].memId);

		CL_Handler.SetKernelArgMem(slicekernel,0,fBufferId);
		CL_Handler.SetKernelArgMem(slicekernel,1,(int)Terrain_Handler.Chunks[0].memId);
		CL_Handler.SetKernelArgValue(slicekernel,2,(int)ix,sizeof(int));
		CL_Handler.SetKernelArgValue(slicekernel,3,Terrain_Handler.Chunks[0].MemLength,sizeof(int));
		CL_Handler.SetKernelArgValue(slicekernel,5,(int)imageSize2.x,sizeof(int));
		CL_Handler.SetKernelArgValue(slicekernel,6,(int)imageSize2.y,sizeof(int));
		//Debug.Log(Terrain_Handler.Chunks[0].MemLength);
		//Debug.Log((int)imageSize2.x);
		CL_Handler.SetKernelArgValue(slicekernel,4,0,sizeof(int));

		globalWorkSize[0] = (int)128;
		globalWorkSize[1] = (int)128;

		CL_Handler.DispatchKernel(slicekernel,2,globalWorkSize,null,false);

		CL_Handler.MemCpy_ClientToHost(fBufferId);
		CL_Handler.MemCpy_ClientToHost((int)Terrain_Handler.Chunks[0].memId);

		int id;
		float val;
		for(int y = 0; y < 128;y++){
			for(int x = 0; x < 128; x++){
				id = (int)(y*128+x);
				val = (float)fBuffer[id]/255.0f;
				if(fBuffer[id] == 255){
					Debug.Log("Yes!");
				}
				CL_Handler.debugTex.SetPixel(x,y,new Color(val,val,val,1));
			}
		}
		CL_Handler.debugTex.Apply();



		//Terrain_Handler.Chunks[0].checkFirst(new Vector3(1,0,0));
		//Terrain_Handler.Chunks[0].checkFirst(new Vector3(2,0,0));

    //block |= lvl<<25;
    //return block&0x00FFFFFF;
	}

	public uint evaluateBlock(int relX, int relY,int relZ,uint* chunk,int lastI){
    Vector3 bPos = new Vector3(0,0,0);
    uint block = chunk[lastI-1];
		Debug.Log("block:"+block);
    uint off = 0;

    uint lvl = 64;//TODO:Hardcoded Max size 128?
    //uint step = 1;

    while((block&0xFF000000) != 0xC0000000){
				Debug.Log("block:"+((block&0xFF000000)==0xC0000000?"L":"N")+" "+(block&0x00FFFFFF)+";");
        off |= (uint)(relX>=(bPos.x+lvl)? 1:0);
        off |= (uint)(relY>=(bPos.y+lvl)? 2:0);
        off |= (uint)(relZ>=(bPos.z+lvl)? 4:0);
				Debug.Log(bPos+" "+lvl);
				Debug.Log("off:"+off);
        block = chunk[(block&0x00FFFFFF)+off];
        bPos += new Vector3((off&1)*lvl,((off&2)>>1)*lvl,((off&4)>>2)*lvl);
        lvl = lvl >> 1;
        off = 0;
        //step = step << 1;
    }
    //block &= 0x00FFFFFF;
    //block |= lvl<<25;
    return block&0x00FFFFFF;
    //return chunk[(int)(relPos.x*128+relPos.y)]&0x00FFFFFF;
}

	public void RenderTerrain(){
		//GL.IssuePluginEvent(CL_Handler.GetRenderEventFunc(), 1);
		//CL_Handler.RenderIt();
		//GL.IssuePluginEvent(0);
		float t = Time.realtimeSinceStartup;
		Ray referenceRay = renderCam.ScreenPointToRay(new Vector3((float)(Screen.width*0.5), (float)(Screen.height*0.5), 0));
		float height = Mathf.Tan(renderCam.fieldOfView*0.5f*Mathf.Deg2Rad)*renderCam.nearClipPlane;
		Vector3 rightVector = (renderCam.transform.right*height*renderCam.aspect);//*(0.5f/Screen.width);
		//Debug.Log((0.5f/Screen.width));
		Vector3 upVector = -(renderCam.transform.up*height);//*(0.5f/Screen.height);
		Vector3 clipOff = renderCam.transform.forward*renderCam.nearClipPlane;

		/** 0-2 Camera Position
			* 3-5 Right Camera Verctor
			* 6-8 Up Camera Vector
			* 9-11 Forward Camera Vector
			* 12-14 Chunk Position
		* */

		initDirection[0] = renderCam.transform.position.x;
		initDirection[1] = renderCam.transform.position.y;
		initDirection[2] = renderCam.transform.position.z;

		initDirection[3] = rightVector.x;
		initDirection[4] = rightVector.y;
		initDirection[5] = rightVector.z;

		initDirection[6] = upVector.x;
		initDirection[7] = upVector.y;
		initDirection[8] = upVector.z;

		Vector3 cPos = new Vector3((float)Terrain_Handler.Chunks[0].pos.x,Terrain_Handler.Chunks[0].pos.y,Terrain_Handler.Chunks[0].pos.z);

		//(49.0, 5.0, 48.0)
		initDirection[9] = renderCam.transform.forward.x;//cPos.r;
		initDirection[10] = renderCam.transform.forward.y;//cPos.g;
		initDirection[11] = renderCam.transform.forward.z;//cPos.b;

		initDirection[12] = 0;//(float)Terrain_Handler.Chunks[0].pos.x;
		initDirection[13] = 0;//(float)Terrain_Handler.Chunks[0].pos.y;
		initDirection[14] = 0;//(float)Terrain_Handler.Chunks[0].pos.z;

		initDirection[15] = clipOff.x;
		initDirection[16] = clipOff.y;
		initDirection[17] = clipOff.z;

		//Debug.Log("ChunkPos: "+Terrain_Handler.Chunks[0].pos);
		//Debug.Log("CameraPos: "+renderCam.transform.position);
		//Debug.Log(renderCam.transform.position+rightVector);
	/*	Debug.DrawLine(renderCam.transform.position+clipOff, clipOff+renderCam.transform.position+rightVector, Color.red);
		Debug.DrawLine(renderCam.transform.position+clipOff, clipOff+renderCam.transform.position+upVector, Color.green);
		Debug.DrawLine(renderCam.transform.position+clipOff, clipOff+renderCam.transform.position+renderCam.transform.forward, Color.yellow);
		Debug.DrawLine(renderCam.ScreenPointToRay(new Vector3(0,0,0)).origin, renderCam.ScreenPointToRay(new Vector3(0,0,0)).origin+renderCam.ScreenPointToRay(new Vector3(0,0,0)).direction, Color.blue);
*/
		//Terrain_Handler.Chunks[0].drawDebugBox(Color.red);

		CL_Handler.MemCpy_HostToClient(initDirectionId);
		CL_Handler.MemCpy_HostToClient((int)Terrain_Handler.Chunks[0].memId);

		//Debug.Log("MemLength "+Terrain_Handler.Chunks[0].MemLength);

		// Frame Buffer, ChunkMemory, ChunkMemory Length, Vector Array, max Depth
		/*Debug.Log("fbid:"+glFrameBufferId);
		CL_Handler.SetKernelArgFB(renderKernel,0,glFrameBufferId);
		CL_Handler.SetKernelArgMem(renderKernel,1,(int)Terrain_Handler.Chunks[0].memId);
		CL_Handler.SetKernelArgValue(renderKernel,2,Terrain_Handler.Chunks[0].MemLength,sizeof(int));
		CL_Handler.SetKernelArgMem(renderKernel,3,initDirectionId);
		CL_Handler.SetKernelArgValue(renderKernel,4,7,sizeof(int));

		CL_Handler.DispatchKernel(renderKernel,2,globalWorkSize,null,true);*/

		//CL_Handler.MemCpy_ClientToHost(fBufferId);
		/*
		float val = 0;
		int id = 0;
		for(int y = 0; y < imageSize2.y;y++){
			for(int x = 0; x < imageSize2.x; x++){
				id = (int)(y*imageSize2.x+x);
				val = ((float)fBuffer[id])/255;
				CL_Handler.debugTex.SetPixel(x,y,new Color(val,val,val,1));
			}
		}
		//renderTestSlice(35);
		CL_Handler.debugTex.Apply();*/
	}

	public void AddToRenderQueue(Vector3 Pos, int Id){
		/*renderQueue[idx*4  ] = Id;
		renderQueue[idx*4+1] = (int)Pos.x;
		renderQueue[idx*4+2] = (int)Pos.y;
		renderQueue[idx*4+3] = (int)Pos.z;*/
		//CL_Handler.MemCpy_HostToClient(renderQueueId);
		idx++;
	}
}
