using UnityEngine;

public class Rotate : MonoBehaviour
{
    void Update() => transform.Rotate(Time.deltaTime * 10, Time.deltaTime * 7, 0);
}
