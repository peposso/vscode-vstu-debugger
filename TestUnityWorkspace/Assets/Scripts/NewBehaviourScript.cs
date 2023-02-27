using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NewBehaviourScript : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        var unmanagedTid = AppDomain.GetCurrentThreadId();
        var tid = System.Threading.Thread.CurrentThread.ManagedThreadId;
        Debug.Log("Hello");
        Debug.Log($"tid={tid}");
        Debug.Log($"unmanagedTid={unmanagedTid + 100}");
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
    }
}