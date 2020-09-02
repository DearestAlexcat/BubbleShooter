using UnityEngine;
using UnityEngine.UI;


public class BallController : MonoBehaviour, System.IComparable<BallController>
{
    public int id = -1;
    public int ballType = -1;

    [HideInInspector]
    public RectTransform rectTransform = null;
    [HideInInspector]
    public SpriteRenderer ballImage = null;
    [HideInInspector]
    public TrailRenderer trailRenderer = null;
    [HideInInspector]
    public Vector3 direction;
    
    public int CompareTo(BallController other)
    {
        if (other.rectTransform.localPosition.y > 
            this.rectTransform.localPosition.y)
            return -1;
        else if (Mathf.Approximately(other.rectTransform.localPosition.y, 
            this.rectTransform.localPosition.y))
            return 0;
        else
            return 1;
    }

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        ballImage = GetComponent<SpriteRenderer>();
        trailRenderer = GetComponent<TrailRenderer>();
    }
}
