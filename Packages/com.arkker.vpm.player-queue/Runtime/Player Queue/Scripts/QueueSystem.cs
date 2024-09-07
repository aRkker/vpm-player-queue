
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Data;
using VRC.SDKBase;
using VRC.Udon;


[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class QueueSystem : UdonSharpBehaviour
{

    [Header("Reference to the player plack prefab which gets added to the queue list")]
    public GameObject playerPlackPrefabReference;

    [Header("Reference to the queue list content object, to which the player placks get added")]
    public Transform queueListContent;

    [Header("Reference to the join/leave button so we can change the text and colour")]
    public UnityEngine.UI.Button joinLeaveButton;

    [Header("Reference to the disable queue button")]
    public UnityEngine.UI.Button disableQueueButton;

    [Header("This is a list of all the players who have moderator rights. CaSe SenSiTivE")]
    public string[] moderators = new string[0];

    [Header("This is how many seconds we wait for a player to rejoin before removing them from the queue. Default is 300 (5 minutes)")]
    public float playerRejoinTime = 300;



    [Header("Internal bits and bobs, DO NOT EDIT")]
    [Header("No, really, don't touch")]
    [Header("If you touch these, you will break the system")]
    [Header("And aint nobody else to blame sans yourself")]


    private string[] playerNames = new string[0];
    [UdonSynced] public string playerNamesString = "";
    private string delimiter = "\u2023";  // Unique delimiter unlikely to be in player names


    [UdonSynced] public bool queueEnabled = true;

    private bool _isMod = false;

    private DataDictionary _playerLeftQueueTimes = new DataDictionary();

    private string SerializePlayerNames(string[] names)
    {
        return string.Join(delimiter, names);
    }

    private string[] DeserializePlayerNames(string namesString)
    {
        if (string.IsNullOrEmpty(namesString))
        {
            return new string[0];
        }
        return namesString.Split(new string[] { delimiter }, System.StringSplitOptions.None);
    }

    void Start()
    {
        if (playerPlackPrefabReference == null)
        {
            Debug.LogError("Player plack prefab reference is not set. Please set it in the inspector");
        }

        // Check if the player is a moderator
        foreach (string mod in moderators)
        {
            if (mod == Networking.LocalPlayer.displayName)
            {
                _isMod = true;
                break;
            }
        }

        if (Networking.IsMaster)
        {
            SendCustomEventDelayedSeconds("LeftPlayerChecker", 10);
        }

        if (_isMod)
        {
            disableQueueButton.gameObject.SetActive(true);
        }
    }

    public void ToggleQueueStatus()
    {
        Debug.Log("Toggle queue status button clicked");
        if (_isMod)
        {
            Networking.SetOwner(Networking.LocalPlayer, gameObject);
            queueEnabled = !queueEnabled;
            disableQueueButton.GetComponentInChildren<TMPro.TextMeshProUGUI>().text = queueEnabled ? "Disable queue" : "Enable queue";
            disableQueueButton.GetComponentInChildren<UnityEngine.UI.Image>().color = queueEnabled ? new Color(0.8f, 0.2f, 0.2f) : new Color(0.4f, 1f, 0.4f);
            RequestSerialization();
            OnDeserialization();
        }
    }

    public override void OnMasterTransferred(VRCPlayerApi newMaster)
    {
        // base.OnMasterTransferred(newMaster);
        if (newMaster.isLocal)
        {
            Debug.Log("I am the new master. Starting the left player checker");
            SendCustomEventDelayedSeconds("LeftPlayerChecker", 10);

        }
    }

    // This function gets called every time a player joins the instance. We need to check if they are in the removed list and remove them if they are
    public override void OnPlayerJoined(VRCPlayerApi player)
    {
        if (_playerLeftQueueTimes.ContainsKey(player.displayName))
        {
            Debug.Log("Player " + player.displayName + " has rejoined the instance, removing them from the removal list");
            _playerLeftQueueTimes.Remove(player.displayName);
        }
    }


    // This function gets called every 10 seconds to check if a player has been gone for too long
    public void LeftPlayerChecker()
    {
        if (!Networking.IsMaster) return;

        int[] playersToRemove = new int[playerNames.Length];
        int removeCount = 0;

        for (int i = 0; i < playerNames.Length; i++)
        {
            string playerName = playerNames[i];
            if (_playerLeftQueueTimes.ContainsKey(playerName) && _playerLeftQueueTimes[playerName].Double < Time.time)
            {
                Debug.Log("Player " + playerName + " has been gone for too long, removing them from the queue");
                playersToRemove[removeCount] = i;
                removeCount++;
            }
        }

        string[] newPlayerNames = new string[playerNames.Length - removeCount];
        int newIndex = 0;

        for (int i = 0; i < playerNames.Length; i++)
        {
            bool shouldRemove = false;

            for (int j = 0; j < removeCount; j++)
            {
                if (i == playersToRemove[j])
                {
                    shouldRemove = true;
                    break;
                }
            }

            if (!shouldRemove)
            {
                newPlayerNames[newIndex] = playerNames[i];
                newIndex++;
            }
        }

        // Only serialize if there is a change in the playerNames array
        if (newPlayerNames.Length != playerNames.Length)
        {
            Debug.Log("Found players to remove, syncing the queue");
            Networking.SetOwner(Networking.LocalPlayer, gameObject);
            playerNamesString = SerializePlayerNames(newPlayerNames);

            RequestSerialization();
            OnDeserialization();
        }

        SendCustomEventDelayedSeconds("LeftPlayerChecker", 10);
    }

    // This function gets called whenever a player leaves the world
    public override void OnPlayerLeft(VRCPlayerApi player)
    {
        // If the player is in the queue, we need to start their removal timer
        for (int i = 0; i < playerNames.Length; i++)
        {
            if (playerNames[i] == player.displayName)
            {
                // We need to store the time when they need to be removed, if they haven't come back by then
                _playerLeftQueueTimes[player.displayName] = Time.time + playerRejoinTime;
            }
        }
    }

    public void ChangeJoinButtonToLeavebutton()
    {
        joinLeaveButton.GetComponentInChildren<TMPro.TextMeshProUGUI>().text = "Leave queue";
        joinLeaveButton.GetComponent<UnityEngine.UI.Image>().color = new Color(0.8f, 0.2f, 0.2f);
    }

    public void ChangeLeaveButtonToJoinButton()
    {
        joinLeaveButton.GetComponentInChildren<TMPro.TextMeshProUGUI>().text = "Sign me up";
        joinLeaveButton.GetComponent<UnityEngine.UI.Image>().color = new Color(0.4f, 1f, 0.4f);
    }

    // This function gets called by the Sign Up / Leave Queue button
    public void SignUpClick()
    {
        Debug.Log("Sign up button clicked");

        /* 

            Here, we check if the player is already in the queue, since this code is used for both signing up and leaving the queue.

        */

        if (playerNames.Length == 0) // If the queue is empty, then obviously they can't be in it
        {
            Debug.Log("Player is not in the queue, adding them");
            _AddLocalPlayer();
            return;
        }
        else
        {
            for (int i = 0; i < playerNames.Length; i++)
            {
                if (playerNames[i] == Networking.LocalPlayer.displayName)
                {
                    Debug.Log("Player is already in the queue, removing them");
                    _RemoveLocalPlayer(i);
                    return;
                }
            }

            Debug.Log("Player is not in the queue, adding them");
            _AddLocalPlayer();
        }
    }



    // This function gets called by the Sign Up / Leave Queue button. The underscore is to indicate that this is a private function, and can not be called over the network.
    // Some silly hackers might try to call this function over the network, but it won't work, because it's underscored.
    private void _AddLocalPlayer()
    {
        if (!queueEnabled)
        {
            Debug.Log("Queue is disabled, not adding player");
            return;
        }
        Debug.Log("Adding local player to the queue");
        string[] newPlayerNames = new string[playerNames.Length + 1]; // Grow the list by 1 to add the new player
        for (int i = 0; i < playerNames.Length; i++)
        {
            newPlayerNames[i] = playerNames[i]; // Copy the existing player names to the new list
        }
        newPlayerNames[playerNames.Length] = Networking.LocalPlayer.displayName; // Add the new player to the end of the list

        // Since we're adding a player to the queue, we need to instantiate a player plack for them
        GameObject newPlayerPlack = Instantiate(playerPlackPrefabReference);

        // Set the player plack's name to the player's name
        newPlayerPlack.GetComponentInChildren<TMPro.TextMeshProUGUI>().text = Networking.LocalPlayer.displayName;

        // Set the player plack's parent to the queue list content object
        newPlayerPlack.transform.SetParent(queueListContent, false);

        // Fetch the players queue number
        int queueNumber = queueListContent.childCount;

        // If the local player is a moderator, lets show the buttons to remove the player from the queue and move them up and down
        if (_isMod)
        {
            newPlayerPlack.transform.Find("Delete").gameObject.SetActive(true);

            // If there are people before this player, show the up arrow
            if (queueNumber > 1)
            {
                newPlayerPlack.transform.Find("UpArrow").gameObject.SetActive(true);
            }

        }

        // Finally, since we want this to be synced to everyone, the local player has to take ownership of the object, and then sync it to everyone
        Networking.SetOwner(Networking.LocalPlayer, gameObject);

        playerNamesString = SerializePlayerNames(newPlayerNames);

        // Sync the player names list to everyone
        RequestSerialization();

        OnDeserialization(); // Call the OnDeserialization function to update the queue list

        ChangeJoinButtonToLeavebutton(); // Change the button text to Leave Queue

    }

    // Again a private function, this time to remove the player from the queue
    // Also underscored, even though it doesn't really matter that much if this one gets called over the network, but its good practices
    // It gets called with one paramter: the index of the player in the player names list
    private void _RemoveLocalPlayer(int index)
    {
        string[] newPlayerNames = new string[playerNames.Length - 1]; // Shrink the list by 1 to remove the player
        int j = 0;
        for (int i = 0; i < playerNames.Length; i++)
        {
            if (i == index)
            {
                continue; // Skip the player we want to remove
            }
            newPlayerNames[j] = playerNames[i]; // Copy the existing player names to the new list
            j++;
        }

        playerNames = newPlayerNames; // Set the new list as the player names list

        // Since we're removing a player from the queue, we need to find the player plack and destroy it
        foreach (Transform child in queueListContent)
        {
            if (child.GetComponentInChildren<TMPro.TextMeshProUGUI>().text == Networking.LocalPlayer.displayName) // Find the player plack with the player's name
            {
                Destroy(child.gameObject); // Destroy the player plack
                break;
            }
        }

        // Finally, since we want this to be synced to everyone, the local player has to take ownership of the object, and then sync it to everyone
        Networking.SetOwner(Networking.LocalPlayer, gameObject);

        playerNamesString = SerializePlayerNames(playerNames);

        // Sync the player names list to everyone
        RequestSerialization();

        OnDeserialization(); // Call the OnDeserialization function to update the queue list

        ChangeLeaveButtonToJoinButton(); // Change the button text to Join Queue
    }

    // The following function gets called whenever something is deserialized (ie, syncrhonized) from somewhere else to the local player
    public override void OnDeserialization()
    {
        Debug.Log("Before deserialization: " + playerNamesString);
        playerNames = DeserializePlayerNames(playerNamesString);

        // This is a pretty heavy thing to do things, but since the queue doesn't change that often, it's fine
        // We are going to destroy all the player placks and then recreate them from the player names list

        foreach (Transform child in queueListContent) // For each child in the queue list content object
        {
            Destroy(child.gameObject); // Destroy the child. Unity will whine about using DestroyImmediate over Destroy, but we need to do this to make sure the child is destroyed before we continue
        }

        SendCustomEventDelayedFrames("ContinueDeserialization", 2); // Call the ContinueDeserialization function after 2 frames so that stuff has been erased from the list properly.
    }

    public void ContinueDeserialization()
    {
        bool localPlayerFound = false; // We need to check if the local player is in the queue

        foreach (string playerName in playerNames) // For each player in the player names list
        {
            GameObject newPlayerPlack = Instantiate(playerPlackPrefabReference); // Instantiate a new player plack
            newPlayerPlack.GetComponentInChildren<TMPro.TextMeshProUGUI>().text = playerName; // Set the player plack's name to the player's name
            newPlayerPlack.transform.SetParent(queueListContent, false); // Set the player plack's parent to the queue list content object

            if (playerName == Networking.LocalPlayer.displayName) // If the player is the local player
            {
                // we need to colour our own plack with 0.5471698, 0.09862823, 0.3921569
                newPlayerPlack.GetComponentInChildren<UnityEngine.UI.Image>().color = new Color(0.3f, 0.8471698f, 0.3f);
                newPlayerPlack.GetComponentInChildren<TMPro.TextMeshProUGUI>().text += " (You)"; // Add a (You) to the player's name
                localPlayerFound = true; // Set the local player found to true
            }
            else
            {
                newPlayerPlack.GetComponentInChildren<UnityEngine.UI.Image>().color = new Color(0.311f, 0.311f, 0.311f);
            }
        }

        // if we are a moderator, we need to show the buttons to remove the player from the queue and move them up and down
        if (_isMod)
        {
            for (int i = 0; i < queueListContent.childCount; i++)
            {
                Transform playerPlack = queueListContent.GetChild(i); // Get the player plack at the index i
                playerPlack.Find("Delete").gameObject.SetActive(true); // Show the delete button

                if (i > 0) // If there are people before this player, show the up arrow
                {
                    Debug.Log("Showing up arrow. i: " + i);
                    playerPlack.Find("UpArrow").gameObject.SetActive(true);
                }

                if (i < queueListContent.childCount - 1) // If there are people after this player, show the down arrow
                {
                    playerPlack.Find("DownArrow").gameObject.SetActive(true);
                }
            }
        }

        // lets check if the join/leave button is on leave text

        if (localPlayerFound)
        {
            ChangeJoinButtonToLeavebutton();
        }
        else
        {
            ChangeLeaveButtonToJoinButton();
        }

        if (!localPlayerFound && !queueEnabled)
        {
            joinLeaveButton.gameObject.SetActive(false);
        }
        else if (!localPlayerFound && queueEnabled)
        {
            // joinLeaveButton.enabled = true;
            joinLeaveButton.gameObject.SetActive(true);

        }
    }

    public void MovePlayerPlackDown(int index)
    {
        // check if the plack can be moved down
        if (index == playerNames.Length - 1)
        {
            return;
        }

        Debug.Log("Moving player plack down. Index: " + index);
        string temp = playerNames[index];
        playerNames[index] = playerNames[index + 1];
        playerNames[index + 1] = temp;

        // Since we're moving a player in the queue, we need to find the player plack and move it down
        Transform playerPlack = queueListContent.GetChild(index);
        playerPlack.SetSiblingIndex(index + 1);

        // Finally, since we want this to be synced to everyone, the local player has to take ownership of the object, and then sync it to everyone
        Networking.SetOwner(Networking.LocalPlayer, gameObject);

        playerNamesString = SerializePlayerNames(playerNames);

        // Sync the player names list to everyone
        RequestSerialization();

        OnDeserialization(); // Call the OnDeserialization function to update the queue list
    }

    public void MovePlayerPlackUp(int index)
    {

        // check if the plack can be moved up
        if (index == 0)
        {
            return;
        }

        Debug.Log("Moving player plack up. Index: " + index);
        string temp = playerNames[index];
        playerNames[index] = playerNames[index - 1];
        playerNames[index - 1] = temp;

        // Since we're moving a player in the queue, we need to find the player plack and move it up
        Transform playerPlack = queueListContent.GetChild(index);
        playerPlack.SetSiblingIndex(index - 1);

        // Finally, since we want this to be synced to everyone, the local player has to take ownership of the object, and then sync it to everyone
        Networking.SetOwner(Networking.LocalPlayer, gameObject);


        playerNamesString = SerializePlayerNames(playerNames);

        // Sync the player names list to everyone
        RequestSerialization();

        OnDeserialization(); // Call the OnDeserialization function to update the queue list
    }

    public void DeleteUserByIndex(int index)
    {
        Debug.Log("Deleting user by index: " + index);
        _RemoveLocalPlayer(index);
    }
}
