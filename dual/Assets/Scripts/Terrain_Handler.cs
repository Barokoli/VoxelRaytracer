using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class Terrain_Handler : MonoBehaviour {

	public int ghost_Seed = 1;
	public static int seed = 1;
	public GameObject anchor;
	//all indexes +1! 0 = empty
	int[,,] ChunkMap = new int[100,10,100];
	//1 Block 10cm^3 1000Blocks 1m^3
	public static Vector3 ChunkSize = new Vector3(128,128,128);
	public static int ChunkSizeCube = (int)(ChunkSize.x*ChunkSize.y*ChunkSize.z);
	//1 Chunk 6.4m^3
	public static List<Chunk> Chunks = new List<Chunk>();
	public static int NoiseSize = 64;
	public static int NoiseSize3 = NoiseSize*NoiseSize*NoiseSize;
	public static int NoiseCount = 8;
	//Rendering
	public int renderRadius = 3;
	public static int ghostRenderRadius = 3;
	GameObject renderAnchor;
	public static int chunkInitKernel1 = -1;
	public static int chunkInitKernel2 = -1;
	public static int chunkMemCpyKernel = -1;
	public static int chunkScanKernel = -1;

	public static int MaxChunkSize = 1024;//TODO: Hardcoded Chunk Size

	public GameObject TestObj;

	public static Renderman renderman;
	// Use this for initialization
	void Awake (){
		seed = ghost_Seed;
	}
	void Start () {
		renderman = new Renderman();
		ghostRenderRadius = renderRadius;
		renderAnchor = anchor;
		Chunks = new List<Chunk>();
		CheckChunks();
		//renderman.RenderTerrain();
	}

	// Update is called once per frame
	void Update () {
		renderman.RenderTerrain();
		//renderman.renderTestSlice((int) (ChunkSize.x*0.5f));
	}

	void CheckChunks(){
		Vector3 opos = 0.15625f*renderAnchor.transform.position;
		opos = new Vector3(Mathf.Floor(opos.x),Mathf.Floor(opos.y),Mathf.Floor(opos.z));
		float x = opos.x-renderRadius;
		float y = opos.y;
		float z = opos.z;
		while (x != opos.x+renderRadius){
			while (new Vector2(x-opos.x,y-opos.y).magnitude <= renderRadius){
				while (new Vector3(x-opos.x,y-opos.y,z-opos.z).magnitude <= renderRadius){
					CheckChunkState((int)x,(int)y,(int)z);
					z++;
				}
				z = opos.z;
				while (new Vector3(x-opos.x,y-opos.y,z-opos.z).magnitude <= renderRadius){
					CheckChunkState((int)x,(int)y,(int)z);
					z--;
				}
				z = opos.z;
				y++;
			}
			y = opos.y;
			while (new Vector2(x-opos.x,y-opos.y).magnitude <= renderRadius){
				while (new Vector3(x-opos.x,y-opos.y,z-opos.z).magnitude <= renderRadius){
					CheckChunkState((int)x,(int)y,(int)z);
					z++;
				}
				z = opos.z;
				while (new Vector3(x-opos.x,y-opos.y,z-opos.z).magnitude <= renderRadius){
					CheckChunkState((int)x,(int)y,(int)z);
					z--;
				}
				z = opos.z;
				y--;
			}
			y = opos.y;
			x++;
		}
	}
	void CheckChunkState(int x, int y, int z){
		if(y < ChunkMap.GetLength(1) && y > 0){
			if(ChunkMap[x,y,z]==0){
				Chunks.Add(new Chunk(new Vector3((float)x,(float)y,(float)z),Chunks.Count));
				ChunkMap[x,y,z] = Chunks.Count;
			}else if(Chunks[ChunkMap[x,y,z]-1].state == (int)ChunkState.Idle){
				Chunks[ChunkMap[x,y,z]-1].render(100);
			}
		}
	}

}
