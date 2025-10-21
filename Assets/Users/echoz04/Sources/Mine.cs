using Units.Logic;
using UnityEngine;

public class Mine : MonoBehaviour
{
    private void OnCollisionEnter(Collision other)
    {
        if (other.gameObject.TryGetComponent(out UnitController controller))
        {
            if (controller.TeamId == 1)
            {
                controller.ReceiveDamage(1000-7);
                gameObject.SetActive(false);
                Debug.Log("Boom");
            }
        }
    }
}
