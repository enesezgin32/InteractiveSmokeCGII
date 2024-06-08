using UnityEngine;

[RequireComponent(typeof(Camera))]
[ExecuteInEditMode]
public class RaymarchCamera : MonoBehaviour
{
    [SerializeField] private Shader _shader;

    public Material _raymarchMaterial
    {
        get
        {
            if (!_raymarchMat && _shader)
            {
                _raymarchMat = new Material(_shader);
                _raymarchMat.hideFlags = HideFlags.HideAndDontSave;
            }
            return _raymarchMat;
        }
    }
    private Material _raymarchMat;
    
    public Camera _camera
    {
        get
        {
            if (!_cam)
            {
                _cam = GetComponent<Camera>();
            }
            return _cam;
        }
    }
    private Camera _cam;

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if(!_raymarchMaterial)
        {
            Graphics.Blit(source, destination);
            return;
        }

        var fc = GetCameraFrustumCorners(_camera);
        _raymarchMaterial.SetVector("_camTL", fc.topLeft);
        _raymarchMaterial.SetVector("_camTR", fc.topRight);
        _raymarchMaterial.SetVector("_camBL", fc.bottomLeft);
        _raymarchMaterial.SetVector("_camBR", fc.bottomRight);
        _raymarchMaterial.SetVector("_camPos", _camera.transform.position);
        _raymarchMaterial.SetMatrix("_camToWorldMatrix", _camera.cameraToWorldMatrix);
        
        RenderTexture.active = destination;
        //push current used mvp matrix to allow as to draw fullscreen quad
        GL.PushMatrix();
        GL.LoadOrtho();
        _raymarchMaterial.SetPass(0);
        GL.Begin(GL.QUADS);
        GL.Vertex3(0.0f, 0.0f, 0.0f);
        GL.Vertex3(1.0f, 0.0f, 0.0f);
        GL.Vertex3(1.0f, 1.0f, 0.0f);
        GL.Vertex3(0.0f, 1.0f, 0.0f);
        GL.End();
        //get back the old mvp matrix
        GL.PopMatrix();
    }

    //find camera near plane corners in camera space
    private FrustumCorners GetCameraFrustumCorners(Camera cam)
    {
        float fov = cam.fieldOfView;
        float aspect = cam.aspect;

        float halfHeight = Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);
        Vector3 toTop = Vector3.up * halfHeight;
        Vector3 toRight = Vector3.right * halfHeight * aspect;

        Vector3 topLeft = -Vector3.forward + toTop - toRight;
        Vector3 topRight = -Vector3.forward + toTop + toRight;
        Vector3 bottomLeft = -Vector3.forward - toTop - toRight;
        Vector3 bottomRight = -Vector3.forward - toTop + toRight;
        
        return new FrustumCorners(topLeft, topRight, bottomLeft, bottomRight); 
    }
    
    private class FrustumCorners
    {
        public Vector3 topLeft;
        public Vector3 topRight;
        public Vector3 bottomLeft;
        public Vector3 bottomRight;
        public FrustumCorners(Vector3 tl, Vector3 tr, Vector3 bl, Vector3 br) 
        {
            topLeft = tl;
            topRight = tr;
            bottomLeft = bl;
            bottomRight = br;
        }
    }
}
