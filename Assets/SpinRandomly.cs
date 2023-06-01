using UnityEngine;

[RequireComponent(typeof(Renderer))]
public class SpinRandomly : MonoBehaviour
{
    private Renderer _renderer;
    private float _speed;
    
    private void Start()
    {
        _renderer = GetComponent<Renderer>();
        float rot = Random.Range(0f, 90f);
        if(Random.Range(0, 2) == 1)
            rot = -rot;
        transform.localEulerAngles += new Vector3(0f, rot ,0f);

        _speed = Random.Range(0.08f, 0.12f);
        if (Random.Range(0, 2) == 1)
            _speed = -_speed;
    }
    
    private void Update()
    {
        //transform.localEulerAngles += new Vector3(0f, speed * Time.deltaTime, 0f);
        _renderer.material.mainTextureOffset += new Vector2(_speed * Time.deltaTime, _speed * Time.deltaTime);
    }
}
