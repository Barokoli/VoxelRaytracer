using UnityEngine;
using System.Collections;

public class CamControl : MonoBehaviour {

	// Use this for initialization
	void Start () {

	}

	public float horizontalSpeed = 2.0F;
  public float verticalSpeed = 2.0F;
  void Update() {
      float h = horizontalSpeed * Input.GetAxis("Mouse X");
      float v = verticalSpeed * Input.GetAxis("Mouse Y");
      transform.Rotate(v, h, 0);
  }
}
