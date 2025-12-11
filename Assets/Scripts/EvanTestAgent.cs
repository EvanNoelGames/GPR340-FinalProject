using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class EvanTestAgent : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BoxCollider2D groupDetector;
    [SerializeField] private RTSAgentRigidbody rigidBody;
    public GameObject leader;
    public List<GameObject> followers = new List<GameObject>();
    private Vector3 waypoint;
    private bool isMoving = false;
    private RTSTile currentTile;
    private RTSTile targetTile;
    private bool isFriendly = true;
    [Header("Values")]
    [SerializeField] private Color enemyColor = Color.white; 
    
    private List<RTSTile> path = new List<RTSTile>();
    
    public event Action<bool, GameObject> Killed =  delegate { };

    public void Setup()
    {
        if (!isFriendly)
        {
            GetComponentInChildren<MeshRenderer>().material.color = enemyColor;
        }

        rigidBody.CollisionEnter += RigidbodyOnCollisionEnter2D;
    }

    private void RigidbodyOnCollisionEnter2D(Collision2D other)
    {
        if (!isFriendly) return;
        
        EvanTestAgent otherAgent = other.gameObject.GetComponentInParent<EvanTestAgent>();
        if (otherAgent && otherAgent.isFriendly != isFriendly)
        {
            Killed.Invoke(isFriendly, gameObject);
            otherAgent.Killed.Invoke(otherAgent.isFriendly, otherAgent.gameObject);
        }
    }

    private void FixedUpdate()
    {
        if (!isMoving) return;
        
        // Move toward target position
        transform.position = Vector3.MoveTowards(transform.position, waypoint, Time.deltaTime);

        // We're really close to the target
        if ((transform.position - waypoint).magnitude < 0.005)
        {
            // Early return if path is modified
            if (path.Count == 0)
                return;
    
            // Pop off top of path
            path.RemoveAt(0);
            if (path.Count == 0)
                return;
            currentTile = path.First();
    
            // Path is over
            if (path.Count == 0)
            {
                isMoving = false;
                transform.position = waypoint;
                return;
            }

            // Get new target
            waypoint = path.First().transform.position + Vector3.back * 3;
            targetTile = path.First();
        }
    }
    
    #region Setter/Getter

    public bool GetIsFriendly()
    {
        return isFriendly;
    }
    
    public void SetIsFriendly(bool value)
    {
        isFriendly = value;
    }
    #endregion
    
    #region Group
    private void OnTriggerEnter2D(Collider2D other)
    {
        //TODO groups don't typically merge
        if (leader) return;
        
        EvanTestAgent otherAgent = other.gameObject.GetComponent<EvanTestAgent>();
        if (otherAgent && otherAgent.isFriendly == isFriendly)
        {
            GameObject otherAgentLeader = otherAgent.GetLeader();
            
            // Other agent doesn't have a leader
            if (!otherAgentLeader)
            {
                Follow(otherAgent.gameObject);
            }
            // Otherwise follow the other agent's leader
            else
            {
                Follow(otherAgentLeader.gameObject);
            }
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        EvanTestAgent otherAgent = other.gameObject.GetComponent<EvanTestAgent>();
        
        // Leader
        if (followers.Count != 0)
        {
            if (followers.Contains(other.gameObject))
            {
                followers.Remove(other.gameObject);
                other.GetComponent<EvanTestAgent>().leader = null;
            }
        }
        // Follower
        else if (otherAgent && otherAgent.isFriendly == isFriendly)
        {
            if (leader)
            {
                // We lost a fellow follower
                if (other.gameObject != leader && otherAgent.leader == leader)
                {
                    List<Collider2D> overlaps = new List<Collider2D>();
                    groupDetector.OverlapCollider(new ContactFilter2D().NoFilter(), overlaps);
                    overlaps.Remove(other);
                    overlaps.Remove(gameObject.GetComponent<BoxCollider2D>());
                    
                    // Check if we are still overlapping someone with the same leader
                    foreach (Collider2D overlap in overlaps)
                    {
                        EvanTestAgent overlapAgent = overlap.GetComponent<EvanTestAgent>();
                        if (overlapAgent && overlapAgent.leader == leader)
                        {
                            return;
                        }
                    }
                    
                    // We lost connection to the leader
                    EvanTestAgent leaderAgent = leader.GetComponent<EvanTestAgent>();
                    leaderAgent.RemoveFollower(gameObject);
                    leader = null;
                }
            }
        }
    }
    
    public void Follow(GameObject newLeader)
    {
        // Don't follow ourselves
        if (newLeader == gameObject) return;
        
        newLeader.GetComponent<EvanTestAgent>().AddFollower(gameObject);
        leader = newLeader;
    }

    public void AddFollower(GameObject newFollower)
    {
        followers.Add(newFollower);
    }
    
    public void RemoveFollower(GameObject oldFollower)
    {
        followers.Remove(oldFollower);
    }

    public GameObject GetLeader()
    {
        return leader;
    }

    public List<GameObject> GetFollowers()
    {
        return followers;
    }
    #endregion
    
    #region Target
    public void SetWaypoint(RTSTile tile)
    {
        waypoint = tile.transform.position + Vector3.back * 3;
        isMoving = true;
    }
    
    public void SetWaypoint(Vector3 position)
    {
        position.z = 0;
        waypoint = position + Vector3.back * 3;
        isMoving = true;
    }

    public void SetPath(List<RTSTile> newPath)
    {
        path = newPath;

        if (path.Count > 0 && path[0] == currentTile)
        {
            path.RemoveAt(0);
        }
        
        if(path.Count > 0)
        {
            targetTile = path[0];
            waypoint = targetTile.transform.position + Vector3.back * 3;
            isMoving = true;
        }
    }

    public void ClearPath()
    {
        isMoving = false;
        path.Clear();
    }

    public bool GetIsMoving()
    {
        return isMoving;
    }
    #endregion
    
    #region Tile
    public void SetCurrentTile(RTSTile tile)
    {
        currentTile = tile;
    }
    
    public RTSTile GetCurrentTile()
    {
        return currentTile;
    }
    #endregion
}
