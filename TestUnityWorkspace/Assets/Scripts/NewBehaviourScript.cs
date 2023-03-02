using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NewBehaviourScript : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        Test();
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    void Test()
    {
        var unmanagedTid = AppDomain.GetCurrentThreadId();
        var tid = System.Threading.Thread.CurrentThread.ManagedThreadId;
        Debug.Log("Hello");
        Debug.Log($"tid={tid}");
        Debug.Log($"unmanagedTid={unmanagedTid}");

        var sum = 0;
        for (var i = 0; i < 10; ++i)
        {
            sum += i;
        }

        Debug.Log($"sum={sum}");
    }
}
