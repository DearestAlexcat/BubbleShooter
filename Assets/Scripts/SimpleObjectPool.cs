using System.Collections.Generic;
using UnityEngine;

public class SimpleObjectPool : MonoBehaviour
{
    [HideInInspector]
    public BallController prefab = null;
    [HideInInspector]
    public Transform bubbleHolder = null;

    Queue<BallController> pool = new Queue<BallController>();

    public void InitializePool(int numberElement)
    {
        if (numberElement <= 0 || prefab == null || bubbleHolder == null)
        {
            return;
        }
        
        BallController gameObject;

        for (int i = 0; i < numberElement; i++)
        {
            gameObject = Instantiate(prefab, bubbleHolder);
            gameObject.ballImage.enabled = false;
            pool.Enqueue(gameObject);
        }
    }

    public void ReturnObject(BallController returnObject)
    {
        if (returnObject != null)
        {
            returnObject.ballImage.enabled = false;
            pool.Enqueue(returnObject);
        }
        else
        {
            Debug.LogError("SimpleObjectPool.ReturnOBject: returnObject is null");
        }
    }

    public BallController GetObject()
    {
        BallController gameObject;

        if (pool.Count > 0)
        {
            gameObject = pool.Dequeue();
        }
        else
        {
            gameObject = Instantiate(prefab, bubbleHolder);
        }
        
        gameObject.ballImage.enabled = true;
        return gameObject;
    }
}
