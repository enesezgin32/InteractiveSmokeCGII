using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

[RequireComponent(typeof(Camera))]
public class Gun : MonoBehaviour
{
    [HideInInspector]public List<BulletHole> BulletHoles;
    private Camera cam;

    public float BulletSize = 0.5f;
    public float BulletHoleWaitTime= 3.0f;
    public float BulletHoleFadeOffAnimationTime = 0.7f;
    
    public int BulletHoleCount => BulletHoles.Count;

    private void Awake()
    {
        BulletHoles = new List<BulletHole>();
        cam = GetComponent<Camera>();
    }
    private void Update()
    {
        if (Input.GetMouseButtonDown(2))
        {
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            var bulletHole = new BulletHole(ray.origin, ray.direction, BulletSize);
            BulletHoles.Add(bulletHole);
            StartCoroutine(BulletHoleAnimation(bulletHole));
        }
    }

    public Vector4[] GetOrigins()
    {
        var origins = new Vector4[100];
        for (int i = 0; i < BulletHoles.Count; i++)
        {
            var hole = BulletHoles[i];
            origins[i] = new Vector4(hole.bulletOrigin.x, hole.bulletOrigin.y, hole.bulletOrigin.z, 0);
        }
        return origins;
    }
    //get directions
    public Vector4[] GetDirections()
    {
        var directions = new Vector4[100];
        for (int i = 0; i < BulletHoles.Count; i++)
        {
            var hole = BulletHoles[i];
            directions[i] = new Vector4(hole.bulletDirection.x, hole.bulletDirection.y, hole.bulletDirection.z, 0);
        }
        return directions;
    }
    //get sizes as float list
    public float[] GetSizes()
    {
        var sizes = new float[100];
        for (int i = 0; i < BulletHoles.Count; i++)
        {
            sizes[i] = BulletHoles[i].size;
        }
        return sizes;
    }

    private IEnumerator BulletHoleAnimation(BulletHole hole)
    {
        yield return new WaitForSeconds(BulletHoleWaitTime);
        DOTween.To(() => hole.size, x => hole.size = x, 0, BulletHoleFadeOffAnimationTime);
        yield return new WaitForSeconds(BulletHoleFadeOffAnimationTime);
        BulletHoles.Remove(hole);
    }
}

public class BulletHole
{
    public Vector3 bulletOrigin;
    public Vector3 bulletDirection;
    public float size;
    public BulletHole(Vector3 origin, Vector3 direction, float size)
    {
        bulletOrigin = origin;
        bulletDirection = direction;
        this.size = size;
        Draw();
    }
    public void Draw()
    {
        Debug.DrawRay(bulletOrigin, bulletDirection * 100, Color.red, 5);
    }
}
