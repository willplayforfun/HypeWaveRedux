using UnityEngine;

public class Spotlight : MonoBehaviour
{
    [SerializeField]
    private Color[] playerColors;

    [SerializeField]
    private SpriteRenderer circle;
    [SerializeField]
    private SpriteRenderer cone;

    internal Transform target;

    internal void SetColor(int playerNum)
    {
        if (playerNum >= 0 && playerNum < playerColors.Length)
        {
            circle.color = playerColors[playerNum];
            cone.color   = playerColors[playerNum];
        }
    }
}
