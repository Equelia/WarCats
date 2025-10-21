using Units.Logic;
using UnityEngine;

public class Dog : MonoBehaviour
{
    [SerializeField] private float _speed = 5f;
    [SerializeField] private float _lifeTime = 5f;

    private void Start()
    {
        Destroy(gameObject, _lifeTime);
    }

    private void Update()
    {
        transform.Translate(Vector3.forward * _speed * Time.deltaTime);
    }

    private void OnCollisionEnter(Collision other)
    {
        if (other.gameObject.TryGetComponent(out UnitController controller))
        {
            if (controller.TeamId == 1)
            {
                controller.ReceiveDamage(1000 - 7);
                Debug.Log("Boom");
                
                gameObject.SetActive(false);
            }
        }
    }
}
