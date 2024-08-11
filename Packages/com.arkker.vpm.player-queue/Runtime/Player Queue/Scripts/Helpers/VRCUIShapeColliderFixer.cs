
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class VRCUIShapeColliderFixer : UdonSharpBehaviour
{
    void Start()
    {
        SendCustomEventDelayedSeconds("FixColliders", 0.5f);
    }

    public void FixColliders()
    {
        BoxCollider boxCollider = GetComponent<BoxCollider>();
        if (boxCollider != null)
        {
            boxCollider.isTrigger = true;
            boxCollider.size = new Vector3(boxCollider.size.x, boxCollider.size.y, 0.01f);
        }
    }
}
