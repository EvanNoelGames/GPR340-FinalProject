using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class RTSGame : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RTSGrid grid;
    [SerializeField] private UICanvas canvas;
    [SerializeField] private Enemy enemyPlayer;
    [SerializeField] private Slider agentCostSlider;
    [SerializeField] private Slider enemySpawnIntervalSlider;
    [SerializeField] private Slider maxEnemiesSlider;
    [SerializeField] private AudioSource killAudioSource;
    [SerializeField] private AudioSource clickAudioSource;
    [SerializeField] private AudioSource newAgentAudioSource;
    [SerializeField] private AudioSource placeAudioSource;
    [Header("Prefabs")]
    [SerializeField] private GameObject agentPrefab;
    [SerializeField] private GameObject waypointPrefab;
    [SerializeField] private TacticalPath _tacticalPath;
    [Header("Values")]
    [SerializeField] private int agentCost = 3;
    [SerializeField] private int startingMoney = 50;
    [SerializeField] private int moneyPerMoneyTile = 5;
    
    private bool isGameRunning = false;
    private bool firstAgentPlaced = false;
    private int money = 0;
    
    private GameObject currentWaypoint = null;
    private List<EvanTestAgent> guidingAgents = new List<EvanTestAgent>();
    private List<EvanTestAgent> activeAgents = new List<EvanTestAgent>();
    
    public List<EvanTestAgent> playerAgents = new List<EvanTestAgent>();
    public List<EvanTestAgent> enemyAgents = new List<EvanTestAgent>();

    private bool isFiring = false;
    
    #region START/UPDATE
    private void Start()
    {
        canvas.RestartGame += ReloadScene;
        grid.MoneyTileEarned += MoneyTileEarned;
        RestartGame();
    }

    private void Update()
    {
        if (!isGameRunning) return;
        CheckForClick();
    }
    
    private void SetupEnemy()
    {
        enemyPlayer.GameStart(startingMoney, agentCost);
    }
    #endregion
    
    #region GAME STATE
    private void ReloadScene()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
    
    private void RestartGame()
    {
        SetupGame();
        StartGame();
    }
    
    private void SetupGame()
    {
        grid.SetupBoard();
        SetMoney(startingMoney);
        canvas.SetAgentCostText(agentCost);
    }

    private void StartGame()
    {
        isGameRunning = true;
        
    }

    private void GameOver(bool playerWon)
    {
        isGameRunning = false;
        canvas.GameOver(playerWon);
    }

    public void AgentKilled(bool friendly, GameObject agent)
    {
        EvanTestAgent agentComponent = agent.GetComponent<EvanTestAgent>();
        activeAgents.Remove(agentComponent);
        
        if (guidingAgents.Contains(agentComponent))
        {
            Destroy(currentWaypoint);
            RTSWaypoint currentWaypointComponent = currentWaypoint.GetComponent<RTSWaypoint>();
        
            grid.NewTileHovered -= currentWaypointComponent.SetTargetTile;
            grid.TileExited -= currentWaypointComponent.ClearTile;
        }
        
        if (friendly)
        {
            playerAgents.Remove(agentComponent);
            killAudioSource.Play();
        }
        else
        {
            enemyAgents.Remove(agentComponent);
        }
        
        Destroy(agent);

        if (playerAgents.Count == 0 && enemyAgents.Count == 0)
        {
            GameOver(money >= enemyPlayer.GetMoney());
        }

        if (playerAgents.Count <= 0)
        {
            GameOver(true);
        }
        else if (enemyAgents.Count <= 0)
        {
            GameOver(false);
        }
    }

    public bool GetIsRunning()
    {
        return isGameRunning;
    }
    #endregion
    
    #region MONEY
    private void MoneyTileEarned(RTSTile tile)
    {
        if (tile.GetOwner() && tile.GetOwner().GetComponent<EvanTestAgent>().GetIsFriendly())
            AddMoney(moneyPerMoneyTile);
        else
        {
            enemyPlayer.AddMoney(moneyPerMoneyTile);
        }
    }
    
    private void SetMoney(int value)
    {
        money = value;
        canvas.SetMoneyText(money);
    }
    
    private void AddMoney(int value)
    {
        money += value;
        canvas.SetMoneyText(money);
    }
    
    private void SubtractMoney(int value)
    {
        money -= value;
        canvas.SetMoneyText(money);
    }
    
    public int GetMoney()
    {
        return money;
    }

    public void ChangeAgentCostSlider()
    {
        agentCost = (int)agentCostSlider.value;
        enemyPlayer.SetAgentCost(agentCost);
        canvas.SetAgentCostText(agentCost);
    }
    #endregion
    
    #region PLAYER INTERACTION
    private void CheckForClick()
    {
        // Left click
        if (Input.GetMouseButtonDown(0))
        {
            Vector2 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            RaycastHit2D[] hits = Physics2D.RaycastAll(mousePos, Vector2.zero);
            RaycastHit2D hit = new RaycastHit2D();
            bool foundHit = false;
            
            foreach (RaycastHit2D hitInfo in hits)
            {
                if (!foundHit && hitInfo.collider.gameObject.CompareTag("Tile"))
                {
                    hit = hitInfo;
                    foundHit = true;
                    break;
                }
            }
            
            if (foundHit)
            {
                RTSTile tileHit = hit.collider.gameObject.GetComponent<RTSTile>();
                
                // Clicked a tile
                if (tileHit)
                {
                    // Clicked tile without owner
                    if (!tileHit.GetOwner() || !tileHit.GetOwner().GetComponent<EvanTestAgent>().GetIsFriendly())
                    {
                        TileClicked(tileHit);
                        return;
                    }
                    
                    EvanTestAgent tileAgent =  tileHit.GetOwner().GetComponent<EvanTestAgent>();
                    
                    // Clicked tile with owner
                    if (tileAgent.GetIsFriendly())
                    {
                        AgentClicked(tileHit.GetOwner().GetComponent<EvanTestAgent>());
                    }
                    else
                    {
                        PlaceWaypoint();;
                    }
                }
                // Check if we clicked an agent
                else
                {
                    EvanTestAgent agentHit = hit.collider.gameObject.GetComponent<EvanTestAgent>();
                    
                    if (agentHit)
                        AgentClicked(agentHit);
                }
            }
        }
    }

    private void TileClicked(RTSTile tile)
    {
        if (!tile) return;
        
        switch (tile.GetTileType())
        {
            case RTSTile.TileType.Blocked:
            {
                return;
            }
            case RTSTile.TileType.Money:
            {
                if (currentWaypoint)
                    PlaceWaypoint(tile);
                return;
            }
            case RTSTile.TileType.None:
            {
                if (currentWaypoint)
                    PlaceWaypoint();
                else
                    SpawnAgentOnTile(tile);
                return;
            }
        }
    }

    private void AgentClicked(EvanTestAgent agent)
    {
        if (currentWaypoint) return;
        
        clickAudioSource.Play();

        // Make sure we are selecting the leader
        if (agent.leader != null)
        {
            agent = agent.leader.GetComponent<EvanTestAgent>();
        }
        
        guidingAgents.Add(agent);

        foreach (GameObject follower in agent.GetFollowers())
        {
            guidingAgents.Add(follower.GetComponent<EvanTestAgent>());
        }
        
        currentWaypoint = Instantiate(waypointPrefab);
        RTSWaypoint newWaypointComponent = currentWaypoint.GetComponent<RTSWaypoint>();
        
        // Allow the waypoint to update while the hovered tile changes
        grid.NewTileHovered += newWaypointComponent.SetTargetTile;
        grid.TileExited += newWaypointComponent.ClearTile;
    }

    private void PlaceWaypoint(RTSTile tile = null)
    {
        if (!currentWaypoint) return;
        
        RTSWaypoint currentWaypointComponent = currentWaypoint.GetComponent<RTSWaypoint>();
        RTSTile targetTile = currentWaypointComponent.GetTargetTileComponent();
        if (!targetTile)
            targetTile = tile;
        if (!targetTile) return;
        
        placeAudioSource.Play();
        
        
        grid.NewTileHovered -= currentWaypointComponent.SetTargetTile;
        grid.TileExited -= currentWaypointComponent.ClearTile;
        
        _tacticalPath = new TacticalPath();
        
        // Placing on tile
        AStar astar = new AStar();
        
        foreach (EvanTestAgent agent in guidingAgents)
        {
           
            if (agent != null)
            {
                List<RTSTile> newPath = astar.FindPath(agent.GetCurrentTile(), targetTile, grid);
                agent.SetPath(newPath);
                targetTile.plannedOwner = agent.gameObject;
            }
            
        }
        
        // else // Placing on enemy agent
        // {
        //     foreach (EvanTestAgent agent in guidingAgents)
        //     {
        //         agent.SetWaypoint(Camera.main.ScreenToWorldPoint(Input.mousePosition));
        //     }
        // }
        
        guidingAgents.Clear();
        Destroy(currentWaypoint);
    }

    private void SpawnAgentOnTile(RTSTile tile)
    {
        newAgentAudioSource.Play();
        
        // Tile is not discovered
        if (tile.GetIsHidden()) return;
        // Not enough money to spawn agent
        if (money < agentCost) return;
        // Someone is walking to the tile
        if (tile.plannedOwner) return;

        if (!firstAgentPlaced)
        {
            firstAgentPlaced = true;
            SetupEnemy();
        }
        
        SubtractMoney(agentCost);
        
        GameObject newAgent = Instantiate(agentPrefab);
        newAgent.transform.position = tile.transform.position + (Vector3.back * 3);

        EvanTestAgent agentComponent = newAgent.GetComponent<EvanTestAgent>();
        foreach (EvanTestAgent agent in playerAgents)
        {
            Collider2D currentCollider = agent.GetComponentInChildren<Collider2D>();
            Physics2D.IgnoreCollision(currentCollider, newAgent.GetComponentInChildren<Collider2D>());
        }
        
        playerAgents.Add(agentComponent);
        agentComponent.Killed += AgentKilled;
        agentComponent.Setup();
        agentComponent.SetCurrentTile(tile);
        activeAgents.Add(agentComponent);
    }

    public List<EvanTestAgent> GetAllAgents()
    {
        return activeAgents;
    }
    #endregion
    
    #region ENEMIES

    public void UpdateMaxEnemies()
    {
        enemyPlayer.SetMaxEnemies((int)maxEnemiesSlider.value);
    }

    public void UpdateEnemySpawnInterval()
    {
        enemyPlayer.SetSpawnInterval(enemySpawnIntervalSlider.value);
    }
    #endregion
}
