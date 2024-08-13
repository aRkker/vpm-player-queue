
using System;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class UserEntryPlack : UdonSharpBehaviour
{
    [NonSerialized] public QueueSystem qs; // Reference to the queue system
    void Start()
    {
        // We need to find the queue system in one of our parent objects. This is a bit of a hack, but it works.

        Transform parent = transform.parent;
        while (parent != null)
        {
            qs = parent.GetComponent<QueueSystem>();
            if (qs != null)
            {
                break;
            }
            parent = parent.parent;
        }

        if (qs == null)
        {
            Debug.LogError("UserEntryPlack: Could not find QueueSystem in parent objects.");
        }

    }

    public void MoveUp()
    {
        // we need to get our index and call it on the queue system
        int index = transform.GetSiblingIndex();
        qs.MovePlayerPlackUp(index);
    }

    public void MoveDown()
    {
        // we need to get our index and call it on the queue system
        int index = transform.GetSiblingIndex();
        qs.MovePlayerPlackDown(index);
    }

    public void Delete()
    {
        // we need to get our index and call it on the queue system
        int index = transform.GetSiblingIndex();
        qs.DeleteUserByIndex(index);
    }
}