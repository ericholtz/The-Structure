using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.Video;

/*
 * Bugs and Documented problems:
 * 
 * Issue #1:
 * When exploring and going back and forth across a boundary,
 * exploring that direction later will result in high amounts of space and lack of walls or floors.
 * This occurs because this UpdateReservations function isn't working correctly.
 * When it shifts over by one, it's deleting a random unimportant row,
 * the row at the end of the array. What instead needs to happen is that the actual
 * values in the array are shifted correctly with the values being left behind getting deleted.
 * As of writing this I'm working on the bug but not exactly sure how to implement a fix.
 * It's a complicated problem. Hopefully future me can figure this out.
 * 
 * Issue #2:
 * When created, ropes and ramps (and future features) will delete each other,
 * because they don't correctly check the area for reservations.
 * For example, ropes spawn and have a for loop that travels vertically through the layers,
 * deleting any floors it comes across, which isn't a problem in a vacuum.
 * However, when ramps get involved, the rope deletes the ramp, but doesn't restore the
 * walls and supports that the ramp destroyed in the MakeWayForRamp() function.
 * This and the opposite situation with ramps deleting ropes leads to large
 * portions of empty space left over in odd places.
 * PARTIALLY SOLVED (kinda): I think I've fixed one instance where ramps would delete ropes.
 * Further testing is required.
 * 
 * Issue #3:
 * Collision physics are kinda odd, the friction is too high overall.
 * Merely bumping into a wall basically freezes you in place.
 * SOLVED (kinda) can't stop falling by moving into a wall anymore, still slows fall considerably
 * 
 * Issue #4:
 * Jumping in certain corners of the tiles creates sections then deletes them, both diagonally.
 * 
 * Issue #5: crossbars on ramps not getting deleted
 * SOLVED (I think): crossbar is created after ramp, so doesn't check reservations; EVERYTHING NEEDS TO CHECK RESERVATIONS
 * 
 * Issue #6: the sticking out bit of 2 ramps can cross if they are perpendicular
 * haven't been able to replicate even with insanely high ramp spawn rates
 * 
 * Issue #7: ramp behavior is still odd, sometimes gravity stops even though not on the ramp itself.
 * 
 * Issue #8: can get softlocked at bottom of ropes
 * SOLVED (mostly): removed all 4 walls at bottom of ropes, should make it much harder to get softlocked, but still possible in rare case
 */


public class Structure : MonoBehaviour
{
    public Rigidbody rb;

    [Header("Structure Dimensions")]
    private GameObject[,,] structure;
    public int structureSideLength;
    public int structureRadius;
    public int smallerRadius;

    [Header("Reserved Spaces")]
    private bool[,,,] reservedStructure;
    public int reservedStructureSideLength;
    public int reservedStructureRadius;

    [Header("Parents")]
    public GameObject tilesParent;
    public GameObject supportsParent;

    [Header("Other Vars")]
    private GameObject[,] supports;

    [Header("Player and calculation floats")]
    private float[] roundedPlayerPosition;
    private float[] playerInTile;
    private float[] boundsX;
    private float[] boundsY;
    private float[] boundsZ;
    private int changeX;
    private int changeY;
    private int changeZ;

    [Header("Tile Dimensions")]
    public float tileLength;

    [Header("Materials")]
    public Material[] mats;
    public Material verticalSupportMaterial;
    public Material horizontalSupportMaterial;
    public int numberOfMaterials;
    public PhysicMaterial supportPhysicMaterial;
    public PhysicMaterial plasticPhysicMaterial;
    public Material ropeMaterial;
    public Material mirrorMaterial;
    public Material plexiglassMaterial;
    public Material[] slideMaterials;
    public Material wallNetMaterial;

    [Header("Meshes")]
    public Mesh cylinderMesh;

    [Header("Lighting")]
    List<GameObject> probes = new List<GameObject>();
    int probeThresholdCount = 0;
    public Camera cam;
    public VideoPlayer video;
    public GameObject flashlight;

    private void Start()
    {
        structure = new GameObject[structureSideLength, structureSideLength, structureSideLength];
        structureRadius = ((structureSideLength + 1) / 2);
        smallerRadius = structureRadius - 1;

        reservedStructureSideLength = (structureSideLength * 2) - 1;
        reservedStructure = new bool[reservedStructureSideLength, reservedStructureSideLength, reservedStructureSideLength, 5];
        reservedStructureRadius = ((reservedStructureSideLength + 1) / 2);

        supports = new GameObject[structureSideLength, structureSideLength];

        if (rb.position.x < 0 || rb.position.y < 0 || rb.position.z < 0)
        {
            roundedPlayerPosition = new float[3] { Mathf.Ceil(rb.position.x), Mathf.Ceil(rb.position.y), Mathf.Ceil(rb.position.z) };
        }
        else
        {
            roundedPlayerPosition = new float[3] { Mathf.Floor(rb.position.x), Mathf.Floor(rb.position.y), Mathf.Floor(rb.position.z) };
        }
        playerInTile = roundedPlayerPosition;

        boundsX = new float[2] { roundedPlayerPosition[0] - (tileLength / 2), roundedPlayerPosition[0] + (tileLength / 2) };
        boundsY = new float[2] { roundedPlayerPosition[1] - (tileLength / 2), roundedPlayerPosition[1] + (tileLength / 2) };
        boundsZ = new float[2] { roundedPlayerPosition[2] - (tileLength / 2), roundedPlayerPosition[2] + (tileLength / 2) };
        
        AddTiles();
        InitializeReservations();
        AddSupports();
        UpdateFloorAndWalls(0, 0, 0);
        MakeTilesInteresting(0, 0, 0);
        //rb.transform.position -= new Vector3(0, tileLength / 2, 0);
    }

    private void Update()
    {
        UpdateStructure();
    }

    private void UpdateStructure()
    {
        changeX = 0;
        changeY = 0;
        changeZ = 0;

        // Update roundedPlayerPosition[]
        roundedPlayerPosition = new float[3];
        if (rb.position.x < 0)
            roundedPlayerPosition[0] = Mathf.Ceil(rb.position.x);
        else
            roundedPlayerPosition[0] = Mathf.Floor(rb.position.x);
        if (rb.position.y < 0)
            roundedPlayerPosition[1] = Mathf.Ceil(rb.position.y);
        else
            roundedPlayerPosition[1] = Mathf.Floor(rb.position.y);
        if (rb.position.z < 0)
            roundedPlayerPosition[2] = Mathf.Ceil(rb.position.z);
        else
            roundedPlayerPosition[2] = Mathf.Floor(rb.position.z);



        // Check if the player has gone past the x or z bounds in either positive or negative direction
        // If so, note that with changeX and changeZ, and then increase bounds
        // Also increase playerInTile positions to match

        // Do so for X's
        if (roundedPlayerPosition[0] < boundsX[0])
        {
            changeX = -1;
            boundsX[0] -= tileLength;
            boundsX[1] -= tileLength;
            playerInTile[0] -= tileLength;
        }
        if (roundedPlayerPosition[0] > boundsX[1])
        {
            changeX = 1;
            boundsX[0] += tileLength;
            boundsX[1] += tileLength;
            playerInTile[0] += tileLength;
        }

        // Do so for Y's
        if (roundedPlayerPosition[1] < boundsY[0])
        {
            changeY = -1;
            boundsY[0] -= tileLength;
            boundsY[1] -= tileLength;
            playerInTile[1] -= tileLength;
        }
        if (roundedPlayerPosition[1] > boundsY[1])
        {
            changeY = 1;
            boundsY[0] += tileLength;
            boundsY[1] += tileLength;
            playerInTile[1] += tileLength;
        }

        // Do so for Z's
        if (roundedPlayerPosition[2] < boundsZ[0])
        {
            changeZ = -1;
            boundsZ[0] -= tileLength;
            boundsZ[1] -= tileLength;
            playerInTile[2] -= tileLength;
        }
        if (roundedPlayerPosition[2] > boundsZ[1])
        {
            changeZ = 1;
            boundsZ[0] += tileLength;
            boundsZ[1] += tileLength;
            playerInTile[2] += tileLength;
        }

        // If player has moved past upper or lower bounds, add tiles in that direction
        if (changeX != 0 || changeY != 0 || changeZ != 0)
        {
            if (changeX != 0)
            {
                UpdateReservations(changeX, 0, 0);
                UpdateTiles(changeX, 0, 0);
                UpdateSupports(changeX, 0, 0);
                UpdateFloorAndWalls(changeX, 0, 0);
                MakeTilesInteresting(changeX, 0, 0);
            }
            if (changeY != 0)
            {
                UpdateReservations(0, changeY, 0);
                UpdateTiles(0, changeY, 0);
                UpdateSupports(0, changeY, 0);
                UpdateFloorAndWalls(0, changeY, 0);
                MakeTilesInteresting(0, changeY, 0);
            }
            if (changeZ != 0)
            {
                UpdateReservations(0, 0, changeZ);
                UpdateTiles(0, 0, changeZ);
                UpdateSupports(0, 0, changeZ);
                UpdateFloorAndWalls(0, 0, changeZ);
                MakeTilesInteresting(0, 0, changeZ);
            }

            ++probeThresholdCount;
            if (probeThresholdCount == smallerRadius)
            {
                probeThresholdCount = 0;
                RenderProbes();
            }
        }
    }

    private void AddTiles()
    {
        for (int i = 0; i < structureSideLength; i++)
        {
            for (int j = 0; j < structureSideLength; j++)
            {
                for (int k = 0; k < structureSideLength; k++)
                {
                    GameObject tile = new GameObject("tile");
                    tile.isStatic = true;
                    float tileX = playerInTile[0] + (tileLength * (i - structureRadius + 1));
                    float tileY = playerInTile[1] + (tileLength * (j - structureRadius + 1));
                    float tileZ = playerInTile[2] + (tileLength * (k - structureRadius + 1));
                    tile.transform.position = new Vector3(tileX, tileY, tileZ);
                    tile.transform.parent = tilesParent.transform;
                    structure[i, j, k] = tile;
                }
            }
        }
    }

    private void UpdateTiles(int x, int y, int z)
    {
        // Destroy old row of tiles
        // Destroy x row
        if (x == 1)
        {
            for (int i = 0; i < structureSideLength; i++)
            {
                for (int j = 0; j < structureSideLength; j++)
                {
                    Destroy(structure[0, i, j]);
                    //Remove(structure[0, i, j]);
                    for (int k = 0; k < structureSideLength - 1; k++)
                    {
                        structure[k, i, j] = structure[k + 1, i, j];
                    }
                }
            }
        }
        if (x == -1)
        {
            for (int i = 0; i < structureSideLength; i++)
            {
                for (int j = 0; j < structureSideLength; j++)
                {
                    Destroy(structure[structureSideLength - 1, i, j]);
                    for (int k = structureSideLength-1; k > 0; k--)
                    {
                        structure[k, i, j] = structure[k - 1, i, j];
                    }
                }
            }
        }

        // Destroy y row
        if (y == 1)
        {
            for (int i = 0; i < structureSideLength; i++)
            {
                for (int j = 0; j < structureSideLength; j++)
                {
                    Destroy(structure[i, 0, j]);
                    for (int k = 0; k < structureSideLength - 1; k++)
                    {
                        structure[i, k, j] = structure[i, k + 1, j];
                    }
                }
            }
        }
        if (y == -1)
        {
            for (int i = 0; i < structureSideLength; i++)
            {
                for (int j = 0; j < structureSideLength; j++)
                {
                    Destroy(structure[i, structureSideLength - 1, j]);
                    for (int k = structureSideLength - 1; k > 0; k--)
                    {
                        structure[i, k, j] = structure[i, k - 1, j];
                    }
                }
            }
        }

        // Destroy z row
        if (z == 1)
        {
            for (int i = 0; i < structureSideLength; i++)
            {
                for (int j = 0; j < structureSideLength; j++)
                {
                    Destroy(structure[i, j, 0]);
                    for (int k = 0; k < structureSideLength - 1; k++)
                    {
                        structure[i, j, k] = structure[i, j, k + 1];
                    }
                }
            }
        }
        if (z == -1)
        {
            for (int i = 0; i < structureSideLength; i++)
            {
                for (int j = 0; j < structureSideLength; j++)
                {
                    Destroy(structure[i, j, structureSideLength - 1]);
                    for (int k = structureSideLength - 1; k > 0; k--)
                    {
                        structure[i, j, k] = structure[i, j, k - 1];
                    }
                }
            }
        }

        // Add new row of tiles
        for (int i = 0; i < structureSideLength; i++)
        {
            for (int j = 0; j < structureSideLength; j++)
            {
                GameObject tile = new GameObject("tile");
                tile.isStatic = true;
                float tileX = playerInTile[0] + (x * tileLength * (structureRadius - 1)) + (tileLength * (i - structureRadius + 1) * Mathf.Abs(y)) + (tileLength * (i - structureRadius + 1) * Mathf.Abs(z));
                float tileY = playerInTile[1] + (y * tileLength * (structureRadius - 1)) + (tileLength * (i - structureRadius + 1) * Mathf.Abs(x)) + (tileLength * (j - structureRadius + 1) * Mathf.Abs(z));
                float tileZ = playerInTile[2] + (z * tileLength * (structureRadius - 1)) + (tileLength * (j - structureRadius + 1) * Mathf.Abs(x)) + (tileLength * (j - structureRadius + 1) * Mathf.Abs(y));
                tile.transform.position = new Vector3(tileX, tileY, tileZ);
                tile.transform.parent = tilesParent.transform;

                // Add new tile to structure array
                // Add for x
                if (x == -1)
                    structure[0, i, j] = tile;
                if (x == 1)
                    structure[structureSideLength - 1, i, j] = tile;
                // Add for y
                if (y == -1)
                    structure[i, 0, j] = tile;
                if (y == 1)
                    structure[i, structureSideLength - 1, j] = tile;
                // Add for z
                if (z == -1)
                    structure[i, j, 0] = tile;
                if (z == 1)
                    structure[i, j, structureSideLength - 1] = tile;
            }
        }
    }

    private void InitializeReservations()
    {
        for (int i = 0; i < reservedStructureSideLength; i++)
        {
            for (int j = 0; j < reservedStructureSideLength; j++)
            {
                for (int k = 0; k < reservedStructureSideLength; k++)
                {
                    for (int l = 0; l < 5; l++)
                    {
                        reservedStructure[i, j, k, l] = false;
                    }
                }
            }
        }
    }

    private void UpdateReservations(int x, int y, int z)
    {
        // Destroy old row of reservations
        // Destroy x row
        if (x == 1)
        {
            for (int i = 0; i < reservedStructureSideLength; i++)
            {
                for (int j = 0; j < reservedStructureSideLength; j++)
                {
                    for (int k = 0; k < reservedStructureSideLength - 1; k++)
                    {
                        for (int n = 0; n < 5; n++)
                        {
                            reservedStructure[k, i, j, n] = reservedStructure[k + 1, i, j, n];
                        }
                    }
                }
            }
        }
        if (x == -1)
        {
            for (int i = 0; i < reservedStructureSideLength; i++)
            {
                for (int j = 0; j < reservedStructureSideLength; j++)
                {
                    for (int k = reservedStructureSideLength - 1; k > 0; k--)
                    {
                        for (int n = 0; n < 5; n++)
                        {
                            reservedStructure[k, i, j, n] = reservedStructure[k - 1, i, j, n];
                        }
                    }
                }
            }
        }

        // Destroy y row
        if (y == 1)
        {
            for (int i = 0; i < reservedStructureSideLength; i++)
            {
                for (int j = 0; j < reservedStructureSideLength; j++)
                {
                    for (int k = 0; k < reservedStructureSideLength - 1; k++)
                    {
                        for (int n = 0; n < 5; n++)
                        {
                            reservedStructure[i, k, j, n] = reservedStructure[i, k + 1, j, n];
                        }
                    }
                }
            }
        }
        if (y == -1)
        {
            for (int i = 0; i < reservedStructureSideLength; i++)
            {
                for (int j = 0; j < reservedStructureSideLength; j++)
                {
                    for (int k = reservedStructureSideLength - 1; k > 0; k--)
                    {
                        for (int n = 0; n < 5; n++)
                        {
                            reservedStructure[i, k, j, n] = reservedStructure[i, k - 1, j, n];
                        }
                    }
                }
            }
        }

        // Destroy z row
        if (z == 1)
        {
            for (int i = 0; i < reservedStructureSideLength; i++)
            {
                for (int j = 0; j < reservedStructureSideLength; j++)
                {
                    for (int k = 0; k < reservedStructureSideLength - 1; k++)
                    {
                        for (int n = 0; n < 5; n++)
                        {
                            reservedStructure[i, j, k, n] = reservedStructure[i, j, k + 1, n];
                        }
                    }
                }
            }
        }
        if (z == -1)
        {
            for (int i = 0; i < reservedStructureSideLength; i++)
            {
                for (int j = 0; j < reservedStructureSideLength; j++)
                {
                    for (int k = reservedStructureSideLength - 1; k > 0; k--)
                    {
                        for (int n = 0; n < 5; n++)
                        {
                            reservedStructure[i, j, k, n] = reservedStructure[i, j, k - 1, n];
                        }
                    }
                }
            }
        }

        // Add new row of false reservations
        for (int i = 0; i < reservedStructureSideLength; i++)
        {
            for (int j = 0; j < reservedStructureSideLength; j++)
            {
                for (int n = 0; n < 5; n++)
                {
                    // Add new reservation to structure array
                    // Add for x
                    if (x == -1)
                        reservedStructure[0, i, j, n] = false;
                    if (x == 1)
                        reservedStructure[reservedStructureSideLength - 1, i, j, n] = false;
                    // Add for y
                    if (y == -1)
                        reservedStructure[i, 0, j, n] = false;
                    if (y == 1)
                        reservedStructure[i, reservedStructureSideLength - 1, j, n] = false;
                    // Add for z
                    if (z == -1)
                        reservedStructure[i, j, 0, n] = false;
                    if (z == 1)
                        reservedStructure[i, j, reservedStructureSideLength - 1, n] = false;
                }
            }
        }
    }

    private void AddSupports()
    {
        for (int i = 0; i < structureSideLength; i++)
        {
            for (int j = 0; j < structureSideLength; j++)
            {
                GameObject support = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                support.isStatic = true;
                float supportX = playerInTile[0] + (tileLength * (i - structureRadius + 1)) - (tileLength / 2);
                float supportZ = playerInTile[2] + (tileLength * (j - structureRadius + 1)) - (tileLength / 2);
                support.transform.position = new Vector3(supportX, playerInTile[1], supportZ);
                support.transform.localScale = new Vector3(0.25f, (structureSideLength * tileLength) / 2, 0.25f);
                support.GetComponent<Renderer>().material = verticalSupportMaterial;
                support.GetComponent<Collider>().material = supportPhysicMaterial;
                support.transform.parent = supportsParent.transform;
                supports[i, j] = support;
            }
        }
    }

    private void UpdateSupports(int x, int y, int z)
    {
        // if y is the only change, move supports up/down and end function
        if (Mathf.Abs(y) == 1)
        {
            for (int i = 0; i < structureSideLength; i++)
            {
                for (int j = 0; j < structureSideLength; j++)
                {
                    supports[i, j].transform.position += new Vector3(0, y * tileLength, 0);
                }
            }
            return;
        }

        // Destroy old row of supports
        if (x == -1)
        {
            for (int i = 0; i < structureSideLength; i++)
            {
                Destroy(supports[structureSideLength - 1, i]);
                for (int j = structureSideLength - 1; j > 0; j--)
                {
                    supports[j, i] = supports[j - 1, i];
                }
            }
        }
        if (x == 1)
        {
            for (int i = 0; i < structureSideLength; i++)
            {
                Destroy(supports[0, i]);
                for (int j = 0; j < structureSideLength - 1; j++)
                {
                    supports[j, i] = supports[j + 1, i];
                }
            }
        }
        if (z == -1)
        {
            for (int i = 0; i < structureSideLength; i++)
            {
                Destroy(supports[i, structureSideLength - 1]);
                for (int j = structureSideLength - 1; j > 0; j--)
                {
                    supports[i, j] = supports[i, j - 1];
                }
            }
        }
        if (z == 1)
        {
            for (int i = 0; i < structureSideLength; i++)
            {
                Destroy(supports[i, 0]);
                for (int j = 0; j < structureSideLength - 1; j++)
                {
                    supports[i, j] = supports[i, j + 1];
                }
            }
        }

        // Create new supports
        for (int i = 0; i < structureSideLength; i++)
        {
            GameObject support = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            support.isStatic = true;
            float supportX = playerInTile[0] + (x * tileLength * (structureRadius - 1)) + (tileLength * (i - structureRadius + 1) * Mathf.Abs(z)) - (tileLength / 2);
            float supportZ = playerInTile[2] + (z * tileLength * (structureRadius - 1)) + (tileLength * (i - structureRadius + 1) * Mathf.Abs(x)) - (tileLength / 2);
            support.transform.position = new Vector3(supportX, playerInTile[1], supportZ);
            support.transform.localScale = new Vector3(0.25f, (structureSideLength * tileLength) / 2, 0.25f);
            support.GetComponent<Renderer>().material = verticalSupportMaterial;
            support.GetComponent<Collider>().material = supportPhysicMaterial;
            support.transform.parent = supportsParent.transform;

            // Add new row of supports to variable supports
            // Add for x
            if (x == -1)
                supports[0, i] = support;
            if (x == 1)
                supports[structureSideLength - 1, i] = support;
            // Add for z
            if (z == -1)
                supports[i, 0] = support;
            if (z == 1)
                supports[i, structureSideLength - 1] = support;
        }
    }

    private void UpdateFloorAndWalls(int x, int y, int z)
    {
        // Do flooring for every GameObject if passed (0, 0, 0)
        if (x == 0 && y == 0 && z == 0)
        {
            for (int i = 0; i < structureSideLength; i++)
            {
                for (int j = 0; j < structureSideLength; j++)
                {
                    for (int k = 0; k < structureSideLength; k++)
                    {
                        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        floor.isStatic = true;
                        floor.layer = LayerMask.NameToLayer("whatIsGround");
                        int materialNumber = Random.Range(0, numberOfMaterials);
                        if (Random.Range(0, 32) == 0)
                            floor.GetComponent<Renderer>().material = plexiglassMaterial;
                        else
                            floor.GetComponent<Renderer>().material = mats[materialNumber];
                        floor.GetComponent<Collider>().material = plasticPhysicMaterial;
                        floor.transform.parent = structure[i, j, k].transform;
                        floor.transform.localScale = new Vector3(tileLength * 0.95f, 0.2f, tileLength * 0.95f);
                        floor.transform.localPosition = new Vector3(0, tileLength / -2, 0);
                        floor.name = "floor";

                        // Create walls
                        GameObject[] walls = new GameObject[2];
                        bool doWall;
                        for (int l = 0; l < 2; l++)
                        {
                            if (Random.Range(0, 2) == 0 && !(i == smallerRadius && j == smallerRadius && k == smallerRadius) && !(i == smallerRadius - 1 && j == smallerRadius && k == smallerRadius && l == 0) && !(i == smallerRadius && j == smallerRadius && k == smallerRadius - 1 && l == 1))
                                doWall = true;
                            else
                                doWall = false;
                            if (doWall)
                            {
                                walls[l] = GameObject.CreatePrimitive(PrimitiveType.Cube);
                                walls[l].isStatic = true;
                                walls[l].transform.localScale = new Vector3(tileLength * 0.9f, tileLength * 0.9f, 0.2f);
                                walls[l].GetComponent<Renderer>().material = wallNetMaterial;
                                walls[l].GetComponent<Collider>().material = plasticPhysicMaterial;
                                materialNumber = Random.Range(0, numberOfMaterials);

                                // create wall attachments
                                bool doSolidWall = false;
                                bool doMirror = false;
                                bool doSlide = false;
                                if (Random.Range(0, 7) == 0)
                                    doSolidWall = true;
                                if (Random.Range(0, 20) == 0)
                                    doMirror = true;
                                if (Random.Range(0, 120) == 0)
                                    doSlide = true;

                                // create solid wall
                                if (doSolidWall)
                                {
                                    // create solid wall
                                    walls[l].GetComponent<Renderer>().material = mats[materialNumber];

                                    // create mirror
                                    if (doMirror)
                                    {
                                        GameObject mirror = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                                        mirror.isStatic = true;
                                        mirror.transform.SetParent(walls[l].transform);
                                        mirror.name = "mirror";
                                        Destroy(mirror.GetComponent<Collider>());
                                        mirror.GetComponent<Renderer>().material = mirrorMaterial;
                                        mirror.transform.localScale = new Vector3(mirror.transform.localScale.x * 1.5f, mirror.transform.localScale.y * 1.2f, mirror.transform.localScale.z / 7.5f);

                                        // rotate mirror
                                        mirror.transform.Rotate(Vector3.right, 90);
                                        mirror.transform.localPosition += new Vector3(0, 0, 0);

                                        // create reflection probe
                                        GameObject reflectionProbe = new GameObject();
                                        reflectionProbe.name = "reflectionProbe";
                                        reflectionProbe.transform.SetParent(mirror.transform);
                                        reflectionProbe.AddComponent<ReflectionProbe>();
                                        reflectionProbe.GetComponent<ReflectionProbe>().mode = UnityEngine.Rendering.ReflectionProbeMode.Realtime;
                                        reflectionProbe.transform.localScale = new Vector3(1, 1, 1);
                                        reflectionProbe.GetComponent<ReflectionProbe>().resolution = 256;
                                        reflectionProbe.GetComponent<ReflectionProbe>().size = new Vector3(1, 1, 1);
                                        reflectionProbe.GetComponent<ReflectionProbe>().backgroundColor = new Color(0.01f, 0.01f, 0.01f);
                                        reflectionProbe.GetComponent<ReflectionProbe>().intensity = 1.7f;
                                        reflectionProbe.GetComponent<ReflectionProbe>().RenderProbe();
                                        probes.Add(reflectionProbe);
                                    }
                                }

                                // create slide meme
                                else if (doSlide)
                                {
                                    walls[l].GetComponent<Renderer>().material = slideMaterials[materialNumber];
                                    walls[l].GetComponent<BoxCollider>().isTrigger = true;
                                    walls[l].AddComponent<PlayBostonCop>();
                                    walls[l].GetComponent<PlayBostonCop>().videoPlayer = video;
                                    walls[l].GetComponent<PlayBostonCop>().cam = cam;
                                    walls[l].GetComponent<PlayBostonCop>().flashlight = flashlight;
                                    walls[l].GetComponent<PlayBostonCop>().player = this.gameObject;

                                    // create non-reversed quad
                                    GameObject correctFace = GameObject.CreatePrimitive(PrimitiveType.Quad);
                                    correctFace.name = "correctFace";
                                    Destroy(correctFace.GetComponent<MeshCollider>());
                                    correctFace.transform.SetParent(walls[l].transform);
                                    correctFace.GetComponent<Renderer>().material = slideMaterials[materialNumber];
                                    correctFace.transform.localScale = Vector3.one;
                                    correctFace.transform.localPosition += new Vector3(0, 0, -0.51f);
                                }
                            }
                            else
                            {
                                walls[l] = new GameObject();
                            }
                            walls[l].name = "wall" + l;
                            walls[l].transform.parent = structure[i, j, k].transform;
                        }
                        walls[0].transform.localPosition = new Vector3(tileLength / 2, 0, 0);
                        walls[0].transform.Rotate(Vector3.up, 90);
                        walls[1].transform.localPosition = new Vector3(0, 0, tileLength / 2);

                        // Create horizontal supports
                        GameObject[] horizontalSupports = new GameObject[2];
                        for (int l = 0; l < 2; l++)
                        {
                            horizontalSupports[l] = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                            horizontalSupports[l].isStatic = true;
                            horizontalSupports[l].transform.localScale = new Vector3(0.25f, tileLength / 2, 0.25f);
                            horizontalSupports[l].GetComponent<Renderer>().material = horizontalSupportMaterial;
                            Destroy(horizontalSupports[l].GetComponent<Collider>());
                            horizontalSupports[l].AddComponent<BoxCollider>();
                            horizontalSupports[l].GetComponent<BoxCollider>().material = plasticPhysicMaterial;
                            horizontalSupports[l].name = "horizontalSupport" + l;
                            horizontalSupports[l].transform.parent = structure[i, j, k].transform;
                            horizontalSupports[l].layer = LayerMask.NameToLayer("whatIsGround");
                        }
                        horizontalSupports[0].transform.localPosition = new Vector3(tileLength / 2, -tileLength / 2 - 0.03125f, 0);
                        horizontalSupports[0].transform.Rotate(Vector3.left, 90);
                        horizontalSupports[1].transform.localPosition = new Vector3(0, -tileLength / 2 - 0.03125f, tileLength / 2);
                        horizontalSupports[1].transform.Rotate(Vector3.forward, 90);

                    }
                }
            }
        }

        // Otherwise, this is being called as an update, and must interpret passed arguments
        else
        {
            for (int i = 0; i < structureSideLength; i++)
            {
                for (int j = 0; j < structureSideLength; j++)
                {
                    // Create floor
                    GameObject floor;
                    int materialNumber;
                    if (!Reserved((structureSideLength / 2) * x + (structureSideLength / 2) * Mathf.Abs(x) + i * Mathf.Abs(y) + i * Mathf.Abs(z), (structureSideLength / 2) * y + (structureSideLength / 2) * Mathf.Abs(y) + i * Mathf.Abs(x) + j * Mathf.Abs(z), (structureSideLength / 2) * z + (structureSideLength / 2) * Mathf.Abs(z) + j * Mathf.Abs(x) + j * Mathf.Abs(y), 0))
                    {
                        floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        floor.isStatic = true;
                        floor.layer = LayerMask.NameToLayer("whatIsGround");
                        materialNumber = Random.Range(0, numberOfMaterials);
                        if (Random.Range(0, 32) == 0)
                            floor.GetComponent<Renderer>().material = plexiglassMaterial;
                        else
                            floor.GetComponent<Renderer>().material = mats[materialNumber];
                        floor.GetComponent<Collider>().material = plasticPhysicMaterial;
                    }
                    else
                    {
                        floor = new GameObject();
                    }
                    floor.transform.parent = structure[(structureSideLength / 2) * x + (structureSideLength / 2) * Mathf.Abs(x) + i * Mathf.Abs(y) + i * Mathf.Abs(z), (structureSideLength / 2) * y + (structureSideLength / 2) * Mathf.Abs(y) + i * Mathf.Abs(x) + j * Mathf.Abs(z), (structureSideLength / 2) * z + (structureSideLength / 2) * Mathf.Abs(z) + j * Mathf.Abs(x) + j * Mathf.Abs(y)].transform;
                    floor.transform.localScale = new Vector3(tileLength * 0.95f, 0.2f, tileLength * 0.95f);
                    floor.transform.localPosition = new Vector3(0, tileLength / -2, 0);
                    floor.name = "floor";

                    // Create walls
                    GameObject[] walls = new GameObject[2];
                    bool doWall;
                    for (int k = 0; k < 2; k++)
                    {
                        if (Random.Range(0, 2) == 0 && !Reserved((structureSideLength / 2) * x + (structureSideLength / 2) * Mathf.Abs(x) + i * Mathf.Abs(y) + i * Mathf.Abs(z), (structureSideLength / 2) * y + (structureSideLength / 2) * Mathf.Abs(y) + i * Mathf.Abs(x) + j * Mathf.Abs(z), (structureSideLength / 2) * z + (structureSideLength / 2) * Mathf.Abs(z) + j * Mathf.Abs(x) + j * Mathf.Abs(y), k + 1))
                            doWall = true;
                        else
                            doWall = false;
                        if (doWall)
                        {
                            walls[k] = GameObject.CreatePrimitive(PrimitiveType.Cube);
                            walls[k].isStatic = true;
                            walls[k].transform.localScale = new Vector3(tileLength * 0.9f, tileLength * 0.9f, 0.2f);
                            walls[k].GetComponent<Renderer>().material = wallNetMaterial;
                            walls[k].GetComponent<Collider>().material = plasticPhysicMaterial;
                            materialNumber = Random.Range(0, numberOfMaterials);

                            // create wall attachments
                            bool doSolidWall = false;
                            bool doMirror = false;
                            bool doSlide = false;
                            if (Random.Range(0, 7) == 0)
                                doSolidWall = true;
                            if (Random.Range(0, 50) == 0)
                                doMirror = true;
                            if (Random.Range(0, 120) == 0)
                                doSlide = true;

                            // create solid wall
                            if (doSolidWall)
                            {
                                // create solid wall
                                walls[k].GetComponent<Renderer>().material = mats[materialNumber];

                                // create mirror
                                if (doMirror)
                                {
                                    GameObject mirror = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                                    mirror.isStatic = true;
                                    mirror.transform.SetParent(walls[k].transform);
                                    mirror.name = "mirror";
                                    Destroy(mirror.GetComponent<Collider>());
                                    mirror.GetComponent<Renderer>().material = mirrorMaterial;
                                    mirror.transform.localScale = new Vector3(mirror.transform.localScale.x * 1.5f, mirror.transform.localScale.y * 1.2f, mirror.transform.localScale.z / 7.5f);

                                    // rotate mirror
                                    mirror.transform.Rotate(Vector3.right, 90);
                                    mirror.transform.localPosition += new Vector3(0, 0, 0);

                                    // create reflection probe
                                    GameObject reflectionProbe = new GameObject();
                                    reflectionProbe.name = "reflectionProbe";
                                    reflectionProbe.transform.SetParent(mirror.transform);
                                    reflectionProbe.AddComponent<ReflectionProbe>();
                                    reflectionProbe.GetComponent<ReflectionProbe>().mode = UnityEngine.Rendering.ReflectionProbeMode.Realtime;
                                    reflectionProbe.transform.localScale = new Vector3(1, 1, 1);
                                    reflectionProbe.GetComponent<ReflectionProbe>().resolution = 256;
                                    reflectionProbe.GetComponent<ReflectionProbe>().size = new Vector3(1, 1, 1);
                                    reflectionProbe.GetComponent<ReflectionProbe>().backgroundColor = new Color(0.01f, 0.01f, 0.01f);
                                    reflectionProbe.GetComponent<ReflectionProbe>().intensity = 1.7f;
                                    reflectionProbe.GetComponent<ReflectionProbe>().RenderProbe();
                                    probes.Add(reflectionProbe);
                                }
                            }

                            // create slide meme
                            else if (doSlide)
                            {
                                walls[k].GetComponent<Renderer>().material = slideMaterials[materialNumber];
                                walls[k].GetComponent<BoxCollider>().isTrigger = true;
                                walls[k].AddComponent<PlayBostonCop>();
                                walls[k].GetComponent<PlayBostonCop>().videoPlayer = video;
                                walls[k].GetComponent<PlayBostonCop>().cam = cam;
                                walls[k].GetComponent<PlayBostonCop>().flashlight = flashlight;
                                walls[k].GetComponent<PlayBostonCop>().player = this.gameObject;

                                // create non-reversed quad
                                GameObject correctFace = GameObject.CreatePrimitive(PrimitiveType.Quad);
                                correctFace.name = "correctFace";
                                Destroy(correctFace.GetComponent<MeshCollider>());
                                correctFace.transform.SetParent(walls[k].transform);
                                correctFace.GetComponent<Renderer>().material = slideMaterials[materialNumber];
                                correctFace.transform.localScale = Vector3.one;
                                correctFace.transform.localPosition += new Vector3(0, 0, -0.51f);
                            }
                        }
                        else
                        {
                            walls[k] = new GameObject();
                        }
                        walls[k].name = "wall" + k;
                        walls[k].transform.parent = structure[(structureSideLength / 2) * x + (structureSideLength / 2) * Mathf.Abs(x) + i * Mathf.Abs(y) + i * Mathf.Abs(z), (structureSideLength / 2) * y + (structureSideLength / 2) * Mathf.Abs(y) + i * Mathf.Abs(x) + j * Mathf.Abs(z), (structureSideLength / 2) * z + (structureSideLength / 2) * Mathf.Abs(z) + j * Mathf.Abs(x) + j * Mathf.Abs(y)].transform;
                    }
                    walls[0].transform.localPosition = new Vector3(tileLength / 2, 0, 0);
                    walls[0].transform.Rotate(Vector3.up, 90);
                    walls[1].transform.localPosition = new Vector3(0, 0, tileLength / 2);

                    // Create horizontal supports
                    GameObject[] horizontalSupports = new GameObject[2];
                    for (int l = 0; l < 2; l++)
                    {
                        if (!Reserved((structureSideLength / 2) * x + (structureSideLength / 2) * Mathf.Abs(x) + i * Mathf.Abs(y) + i * Mathf.Abs(z), (structureSideLength / 2) * y + (structureSideLength / 2) * Mathf.Abs(y) + i * Mathf.Abs(x) + j * Mathf.Abs(z), (structureSideLength / 2) * z + (structureSideLength / 2) * Mathf.Abs(z) + j * Mathf.Abs(x) + j * Mathf.Abs(y), l + 3))
                        {
                            horizontalSupports[l] = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                            horizontalSupports[l].isStatic = true;
                            horizontalSupports[l].GetComponent<Renderer>().material = horizontalSupportMaterial;
                            Destroy(horizontalSupports[l].GetComponent<Collider>());
                            horizontalSupports[l].AddComponent<BoxCollider>();
                            horizontalSupports[l].GetComponent<BoxCollider>().material = plasticPhysicMaterial;
                            horizontalSupports[l].layer = LayerMask.NameToLayer("whatIsGround");
                        }
                        else
                        {
                            horizontalSupports[l] = new GameObject();
                        }
                        horizontalSupports[l].transform.localScale = new Vector3(0.25f, tileLength / 2, 0.25f);
                        horizontalSupports[l].name = "horizontalSupport" + l;
                        horizontalSupports[l].transform.parent = structure[(structureSideLength / 2) * x + (structureSideLength / 2) * Mathf.Abs(x) + i * Mathf.Abs(y) + i * Mathf.Abs(z), (structureSideLength / 2) * y + (structureSideLength / 2) * Mathf.Abs(y) + i * Mathf.Abs(x) + j * Mathf.Abs(z), (structureSideLength / 2) * z + (structureSideLength / 2) * Mathf.Abs(z) + j * Mathf.Abs(x) + j * Mathf.Abs(y)].transform;
                    }
                    horizontalSupports[0].transform.localPosition = new Vector3(tileLength / 2, -tileLength / 2 - 0.03125f, 0);
                    horizontalSupports[0].transform.Rotate(Vector3.left, 90);
                    horizontalSupports[1].transform.localPosition = new Vector3(0, -tileLength / 2 - 0.03125f, tileLength / 2);
                    horizontalSupports[1].transform.Rotate(Vector3.forward, 90);
                }
            }
        }
    }
    private void MakeTilesInteresting(int x, int y, int z)
    {
        // Do process for every GameObject if passed (0, 0, 0)
        if (x == 0 && y == 0 && z == 0)
        {
            for (int i = 0; i < structureSideLength; i++)
            {
                for (int j = 0; j < structureSideLength; j++)
                {
                    for (int k = 0; k < structureSideLength; k++)
                    {
                        // Get floor
                        GameObject floor = structure[i, j, k].transform.GetChild(0).gameObject;

                        // Decide bools
                        bool doRope = false;
                        bool doRamp = false;
                        if (Random.Range(0, 32) == 0 && !(i == smallerRadius || k == smallerRadius))
                        {
                            doRope = true;
                            doRamp = false;
                        }
                        else
                        {
                            doRope = false;

                            if (Random.Range(0, 32) == 0 && floor.GetComponent<MeshRenderer>() != null && floor.GetComponent<MeshRenderer>().material.name != "Plexiglass (Instance)" && !((i == smallerRadius - 1 || i == smallerRadius || i == smallerRadius + 1) && (j == smallerRadius - 1 || j == smallerRadius || j == smallerRadius + 1) && (k == smallerRadius - 1 || k == smallerRadius || k == smallerRadius + 1)))
                                doRamp = true;
                            else
                                doRamp = false;
                        }

                        if (doRope)
                        {
                            // Test if rope will work
                            bool IsSpaceForRope = true;
                            int ropeLength = Random.Range(2, smallerRadius);
                            for (int floorIterator = 0; floorIterator < ropeLength - 1; floorIterator++)
                            {
                                if (Reserved(i, j - floorIterator, k, 0))
                                    IsSpaceForRope = false;
                            }
                            // Check wall reservations at bottom
                            if (Reserved(i, j - ropeLength + 1, k, 1))
                                IsSpaceForRope = false;
                            if (Reserved(i, j - ropeLength + 1, k, 2))
                                IsSpaceForRope = false;
                            // Remove and reserve intersecting floors
                            if (IsSpaceForRope)
                            {
                                Reserve(i, j, k, 0);
                                for (int floorIterator = 1; floorIterator < ropeLength - 1; floorIterator++)
                                {
                                    DestroyBoxComponents(i, j - floorIterator, k, 0);
                                    Reserve(i, j - floorIterator, k, 0);
                                }
                                // Remove and reserve bottom tile walls
                                Reserve(i, j - ropeLength + 1, k, 1);
                                Reserve(i, j - ropeLength + 1, k, 2);
                                Reserve(i - 1, j - ropeLength + 1, k, 1);
                                Reserve(i, j - ropeLength + 1, k - 1, 2);
                                DestroyBoxComponents(i, j - ropeLength + 1, k, 1);
                                DestroyBoxComponents(i, j - ropeLength + 1, k, 2);
                                DestroyBoxComponents(i - 1, j - ropeLength + 1, k, 1);
                                DestroyBoxComponents(i, j - ropeLength + 1, k - 1, 2);

                                // Create parent rope object
                                GameObject ropeParent = new GameObject();
                                ropeParent.name = "rope";
                                ropeParent.transform.parent = structure[i, j, k].transform;
                                ropeParent.transform.SetAsFirstSibling();
                                ropeParent.transform.localPosition = new Vector3(0, (-tileLength * ropeLength / 2) + (tileLength / 2) + (tileLength / 8), 0);

                                // Change the current floor tile to the inside visible rope mesh
                                floor.name = "inside";
                                floor.transform.parent = ropeParent.transform;
                                Destroy(floor.GetComponent<BoxCollider>());
                                floor.GetComponent<MeshFilter>().mesh = cylinderMesh;
                                floor.GetComponent<MeshRenderer>().material = ropeMaterial;
                                floor.transform.localPosition = Vector3.zero;
                                floor.transform.localScale = new Vector3(0.15f, (tileLength * ropeLength / 2) - (tileLength / 8), 0.15f);

                                // Create hitbox
                                GameObject floorHitbox = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                                floorHitbox.name = "hitbox";
                                floorHitbox.transform.parent = ropeParent.transform;
                                Destroy(floorHitbox.GetComponent<MeshRenderer>());
                                floorHitbox.AddComponent<PlayerClimb>();
                                Destroy(floorHitbox.GetComponent<CapsuleCollider>());
                                floorHitbox.AddComponent<MeshCollider>();
                                floorHitbox.GetComponent<MeshCollider>().sharedMesh = cylinderMesh;
                                floorHitbox.GetComponent<MeshCollider>().convex = true;
                                floorHitbox.GetComponent<MeshCollider>().isTrigger = true;
                                floorHitbox.transform.localPosition = Vector3.zero;
                                floorHitbox.transform.localScale = new Vector3(1, floor.transform.localScale.y, 1);
                            }
                        }

                        // Ramps or not
                        if (doRamp)
                        {
                            int doXorZ = Random.Range(0, 2);
                            int tiltX;
                            int tiltZ;
                            if (doXorZ == 0)
                            {
                                tiltX = Random.Range(0, 2);
                                if (tiltX == 0) tiltX = -1; // this makes tiltX be either +1 or -1
                                tiltZ = 0;
                            }
                            else
                            {
                                tiltZ = Random.Range(0, 2);
                                if (tiltZ == 0) tiltZ = -1; // this makes tiltZ be either +1 or -1
                                tiltX = 0;
                            }
                            if (CheckRampReservations(x, y, z, tiltX, tiltZ))
                                continue;
                            floor.name = "ramp";
                            floor.transform.localScale = new Vector3(tileLength * 1.4f * 0.95f, 0.2f, tileLength * 1.4f * 0.95f);
                            floor.transform.localPosition = new Vector3(0, 0, 0);
                            floor.transform.Rotate(tiltX * -26.57f, 0, tiltZ * 26.57f);

                            if (doXorZ == 0)
                            {
                                floor.transform.localScale = new Vector3(tileLength, 0.2f, tileLength * 2.24f * 0.98f);
                                floor.transform.localPosition = new Vector3(0, 0, (tileLength / 2) * tiltX);
                            }
                            else
                            {
                                floor.transform.localScale = new Vector3(tileLength * 2.24f * 0.98f, 0.2f, tileLength);
                                floor.transform.localPosition = new Vector3((tileLength / 2) * tiltZ, 0, 0);
                            }

                            MakeWayForRamp(i, j, k, tiltX, tiltZ);
                        }
                    }
                }
            }
        }

        // Otherwise, this is being called as an update, and must interpret passed arguments
        else
        {
            for (int i = 0; i < structureSideLength; i++)
            {
                for (int j = 0; j < structureSideLength; j++)
                {
                    // Create floor
                    int currentX = (structureSideLength / 2) * x + (structureSideLength / 2) * Mathf.Abs(x) + i * Mathf.Abs(y) + i * Mathf.Abs(z);
                    int currentY = (structureSideLength / 2) * y + (structureSideLength / 2) * Mathf.Abs(y) + i * Mathf.Abs(x) + j * Mathf.Abs(z);
                    int currentZ = (structureSideLength / 2) * z + (structureSideLength / 2) * Mathf.Abs(z) + j * Mathf.Abs(x) + j * Mathf.Abs(y);
                    GameObject floor = structure[currentX, currentY, currentZ].transform.GetChild(0).gameObject;

                    // Decide bools
                    bool doRope;
                    bool doRamp;
                    if (Random.Range(0, 32) == 0 && structure[currentX, currentY, currentZ].transform.GetChild(0).gameObject.GetComponent<MeshFilter>() != null)
                    {
                        doRope = true;
                        doRamp = false;
                    }
                    else
                    {
                        doRope = false;

                        if (Random.Range(0, 32) == 0 && floor.GetComponent<MeshRenderer>() != null && floor.GetComponent<MeshRenderer>().material.name != "Plexiglass (Instance)")
                            doRamp = true;
                        else
                            doRamp = false;
                    }

                    if (doRope)
                    {
                        // Test if rope will work
                        bool IsSpaceForRope = true;
                        int ropeLength = Random.Range(2, smallerRadius);
                        for (int floorIterator = 0; floorIterator < ropeLength - 1; floorIterator++)
                        {
                            if (Reserved(currentX, currentY - floorIterator, currentZ, 0))
                            {
                                IsSpaceForRope = false;
                            }
                        }
                        // Check wall reservations at bottom
                        if (Reserved(currentX, currentY - ropeLength + 1, currentZ, 1))
                            IsSpaceForRope = false;
                        if (Reserved(currentX, currentY - ropeLength + 1, currentZ, 2))
                            IsSpaceForRope = false;
                        // Remove and reserve intersecting floors
                        if (IsSpaceForRope)
                        {
                            Reserve(currentX, currentY, currentZ, 0);
                            for (int floorIterator = 1; floorIterator < ropeLength - 1; floorIterator++)
                            {
                                DestroyBoxComponents(currentX, currentY - floorIterator, currentZ, 0);
                                Reserve(currentX, currentY - floorIterator, currentZ, 0);
                            }
                            // Remove and reserve bottom tcurrentXle walls
                            Reserve(currentX, currentY - ropeLength + 1, currentZ, 1);
                            Reserve(currentX, currentY - ropeLength + 1, currentZ, 2);
                            Reserve(currentX - 1, currentY - ropeLength + 1, currentZ, 1);
                            Reserve(currentX, currentY - ropeLength + 1, currentZ - 1, 2);
                            DestroyBoxComponents(currentX, currentY - ropeLength + 1, currentZ, 1);
                            DestroyBoxComponents(currentX, currentY - ropeLength + 1, currentZ, 2);
                            DestroyBoxComponents(currentX - 1, currentY - ropeLength + 1, currentZ, 1);
                            DestroyBoxComponents(currentX, currentY - ropeLength + 1, currentZ - 1, 2);

                            // Create parent rope object
                            GameObject ropeParent = new GameObject();
                            ropeParent.name = "rope";
                            ropeParent.transform.parent = structure[currentX, currentY, currentZ].transform;
                            ropeParent.transform.SetAsFirstSibling();
                            ropeParent.transform.localPosition = new Vector3(0, (-tileLength * ropeLength / 2) + (tileLength / 2) + (tileLength / 8), 0);

                            // Change the current floor tile to the inside visible rope mesh
                            floor.name = "inside";
                            floor.transform.parent = ropeParent.transform;
                            Destroy(floor.GetComponent<BoxCollider>());
                            floor.GetComponent<MeshFilter>().mesh = cylinderMesh;
                            floor.GetComponent<MeshRenderer>().material = ropeMaterial;
                            floor.transform.localPosition = Vector3.zero;
                            floor.transform.localScale = new Vector3(0.15f, (tileLength * ropeLength / 2) - (tileLength / 8), 0.15f);

                            // Create hitbox
                            GameObject floorHitbox = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                            floorHitbox.name = "hitbox";
                            floorHitbox.transform.parent = ropeParent.transform;
                            Destroy(floorHitbox.GetComponent<MeshRenderer>());
                            floorHitbox.AddComponent<PlayerClimb>();
                            Destroy(floorHitbox.GetComponent<CapsuleCollider>());
                            floorHitbox.AddComponent<MeshCollider>();
                            floorHitbox.GetComponent<MeshCollider>().sharedMesh = cylinderMesh;
                            floorHitbox.GetComponent<MeshCollider>().convex = true;
                            floorHitbox.GetComponent<MeshCollider>().isTrigger = true;
                            floorHitbox.transform.localPosition = Vector3.zero;
                            floorHitbox.transform.localScale = new Vector3(1, floor.transform.localScale.y, 1);
                        }
                    }

                    // Ramps or not
                    if (doRamp)
                    {
                        int doXorZ = Random.Range(0, 2);
                        int tiltX;
                        int tiltZ;
                        if (doXorZ == 0)
                        {
                            tiltX = Random.Range(0, 2);
                            if (tiltX == 0) tiltX = -1; // this makes tiltX be either +1 or -1
                            tiltZ = 0;
                        }
                        else
                        {
                            tiltZ = Random.Range(0, 2);
                            if (tiltZ == 0) tiltZ = -1; // this makes tiltZ be either +1 or -1
                            tiltX = 0;
                        }
                        if (CheckRampReservations(currentX, currentY, currentZ, tiltX, tiltZ))
                            continue;
                        floor.name = "ramp";
                        floor.transform.localScale = new Vector3(tileLength * 1.4f * 0.95f, 0.2f, tileLength * 1.4f * 0.95f);
                        floor.transform.localPosition = new Vector3(0, 0, 0);
                        floor.transform.Rotate(tiltX * -26.57f, 0, tiltZ * 26.57f);

                        if (doXorZ == 0)
                        {
                            floor.transform.localScale = new Vector3(tileLength, 0.2f, tileLength * 2.24f * 0.98f);
                            floor.transform.localPosition = new Vector3(0, 0, (tileLength / 2) * tiltX);
                        }
                        else
                        {
                            floor.transform.localScale = new Vector3(tileLength * 2.24f * 0.98f, 0.2f, tileLength);
                            floor.transform.localPosition = new Vector3((tileLength / 2) * tiltZ, 0, 0);
                        }

                        MakeWayForRamp(currentX, currentY, currentZ, tiltX, tiltZ);
                    }
                }
            }
        }
    }

    private void Reserve(int x, int y, int z, int n)
    {
        x = SmallToBigConvert(x);
        y = SmallToBigConvert(y);
        z = SmallToBigConvert(z);
        if (0 <= x && x < reservedStructureSideLength && 0 <= y && y < reservedStructureSideLength && 0 <= z && z < reservedStructureSideLength)
        {
            reservedStructure[x, y, z, n] = true;
        }
    }

    private void Unreserve(int x, int y, int z, int n)
    {
        x = SmallToBigConvert(x);
        y = SmallToBigConvert(y);
        z = SmallToBigConvert(z);
        if (0 <= x && x < reservedStructureSideLength && 0 <= y && y < reservedStructureSideLength && 0 <= z && z < reservedStructureSideLength)
        {
            reservedStructure[x, y, z, n] = false;
        }
    }
    
    private bool Reserved(int x, int y, int z, int n)
    {
        x = SmallToBigConvert(x);
        y = SmallToBigConvert(y);
        z = SmallToBigConvert(z);
        return reservedStructure[x, y, z, n];
    }

    private bool CheckRampReservations(int x, int y, int z, int tiltX, int tiltZ)
    {
        // Check floor tile
        bool result = Reserved(x, y, z, 0);

        // Check walls surrounding tile
        result = result || Reserved(x, y, z, 1);
        result = result || Reserved(x, y, z, 2);

        // Check ceiling directly above tile
        result = result || Reserved(x, y + 1, z, 0);

        // Check extra wall tiles
        result = result || Reserved(x - 1, y, z, 1);
        result = result || Reserved(x, y, z - 1, 2);

        // Check conditional tiles
        if (tiltX == -1)
        {
            result = result || Reserved(x, y, z - 1, 1);
            result = result || Reserved(x - 1, y, z - 1, 1);
            result = result || Reserved(x, y + 1, z - 1, 0);
            result = result || Reserved(x, y, z - 1, 0);
            result = result || Reserved(x, y + 1, z - 2, 2);

            // Check crossbars
            result = result || Reserved(x, y + 1, z - 1, 4);
        }
        if (tiltX == 1)
        {
            result = result || Reserved(x, y, z + 1, 1);
            result = result || Reserved(x - 1, y, z + 1, 1);
            result = result || Reserved(x, y + 1, z + 1, 0);
            result = result || Reserved(x, y, z + 1, 0);
            result = result || Reserved(x, y + 1, z + 1, 2);

            // Check crossbars
            result = result || Reserved(x, y + 1, z, 4);
        }
        if (tiltZ == -1)
        {
            result = result || Reserved(x - 1, y, z, 2);
            result = result || Reserved(x - 1, y, z - 1, 2);
            result = result || Reserved(x - 1, y + 1, z, 0);
            result = result || Reserved(x - 1, y, z, 0);
            result = result || Reserved(x - 2, y + 1, z, 1);

            // Check crossbars
            result = result || Reserved(x - 1, y + 1, z, 3);
        }
        if (tiltZ == 1)
        {
            result = result || Reserved(x + 1, y, z, 2);
            result = result || Reserved(x + 1, y, z - 1, 2);
            result = result || Reserved(x + 1, y + 1, z, 0);
            result = result || Reserved(x + 1, y, z, 0);
            result = result || Reserved(x + 1, y + 1, z, 1);

            // Check crossbars
            result = result || Reserved(x, y + 1, z, 3);
        }

        return result;
    }

    private void MakeWayForRamp(int x, int y, int z, int tiltX, int tiltZ)
    {
        // Destroy and reserve walls surrounding tile
        DestroyBoxComponents(x, y, z, 1);
        DestroyBoxComponents(x, y, z, 2);
        Reserve(x, y, z, 1);
        Reserve(x, y, z, 2);

        // Destroy and reserve ceiling directly above tile
        DestroyBoxComponents(x, y + 1, z, 0);
        Reserve(x, y + 1, z, 0);

        // Destroy and reserve extra wall tiles
        DestroyBoxComponents(x - 1, y, z, 1);
        DestroyBoxComponents(x, y, z - 1, 2);
        Reserve(x - 1, y, z, 1);
        Reserve(x, y, z - 1, 2);

        // Destroy and reserve conditional tiles
        if (tiltX == -1)
        {
            DestroyBoxComponents(x, y, z - 1, 1);
            DestroyBoxComponents(x - 1, y, z - 1, 1);
            DestroyBoxComponents(x, y + 1, z - 1, 0);
            DestroyBoxComponents(x, y + 1, z - 2, 2);
            Reserve(x, y, z - 1, 1);
            Reserve(x - 1, y, z - 1, 1);
            Reserve(x, y + 1, z - 1, 0);
            Reserve(x, y + 1, z - 2, 2);

            // Destroy and reserve crossbars
            DestroyHorizontalSupportComponents(x, y + 1, z - 1, 4);
            Reserve(x, y + 1, z - 1, 4);
        }
        if (tiltX == 1)
        {
            DestroyBoxComponents(x, y, z + 1, 1);
            DestroyBoxComponents(x - 1, y, z + 1, 1);
            DestroyBoxComponents(x, y + 1, z + 1, 0);
            DestroyBoxComponents(x, y + 1, z + 1, 2);
            Reserve(x, y, z + 1, 1);
            Reserve(x - 1, y, z + 1, 1);
            Reserve(x, y + 1, z + 1, 0);
            Reserve(x, y + 1, z + 1, 2);

            // Destroy and reserve crossbars
            DestroyHorizontalSupportComponents(x, y + 1, z, 4);
            Reserve(x, y + 1, z, 4);
        }
        if (tiltZ == -1)
        {
            DestroyBoxComponents(x - 1, y, z, 2);
            DestroyBoxComponents(x - 1, y, z - 1, 2);
            DestroyBoxComponents(x - 1, y + 1, z, 0);
            DestroyBoxComponents(x - 2, y + 1, z, 1);
            Reserve(x - 1, y, z, 2);
            Reserve(x - 1, y, z - 1, 2);
            Reserve(x - 1, y + 1, z, 0);
            Reserve(x - 2, y + 1, z, 1);

            // Destroy and reserve crossbars
            DestroyHorizontalSupportComponents(x - 1, y + 1, z, 3);
            Reserve(x - 1, y + 1, z, 3);
        }
        if (tiltZ == 1)
        {
            DestroyBoxComponents(x + 1, y, z, 2);
            DestroyBoxComponents(x + 1, y, z - 1, 2);
            DestroyBoxComponents(x + 1, y + 1, z, 0);
            DestroyBoxComponents(x + 1, y + 1, z, 1);
            Reserve(x + 1, y, z, 2);
            Reserve(x + 1, y, z - 1, 2);
            Reserve(x + 1, y + 1, z, 0);
            Reserve(x + 1, y + 1, z, 1);

            // Destroy and reserve crossbars
            DestroyHorizontalSupportComponents(x, y + 1, z, 3);
            Reserve(x, y + 1, z, 3);
        }
    }

    private void DestroyBoxComponents(int x, int y, int z, int n)
    {
        if (0 <= x && x < structureSideLength && 0 <= y && y < structureSideLength && 0 <= z && z < structureSideLength && structure[x, y, z].transform.GetChild(n).gameObject.GetComponent<MeshFilter>() != null)
        {
            Destroy(structure[x, y, z].transform.GetChild(n).gameObject.GetComponent<MeshFilter>());
            Destroy(structure[x, y, z].transform.GetChild(n).gameObject.GetComponent<BoxCollider>());
            Destroy(structure[x, y, z].transform.GetChild(n).gameObject.GetComponent<MeshRenderer>());

            // make mirrors dissapear
            if (n == 1 || n == 2)
            {
                Transform wall = structure[x, y, z].transform.GetChild(n);
                if (wall.childCount != 0)
                {
                    if (wall.GetChild(0).name == "mirror")
                    {
                        wall.GetChild(0).GetComponent<Renderer>().enabled = false;
                    }
                }    
            }
        }
    }

    private void DestroyHorizontalSupportComponents(int x, int y, int z, int n)
    {
        if (0 <= x && x < structureSideLength && 0 <= y && y < structureSideLength && 0 <= z && z < structureSideLength && structure[x, y, z].transform.GetChild(n).gameObject.GetComponent<MeshFilter>() != null)
        {
            Destroy(structure[x, y, z].transform.GetChild(n).gameObject.GetComponent<MeshFilter>());
            Destroy(structure[x, y, z].transform.GetChild(n).gameObject.GetComponent<BoxCollider>());
            Destroy(structure[x, y, z].transform.GetChild(n).gameObject.GetComponent<MeshRenderer>());
            return;
        }
    }

    private int SmallToBigConvert(int x)
    {
        return (x + smallerRadius);
    }
    private int BigToSmallConvert(int x)
    {
        return (x - smallerRadius);
    }

    private void Remove(GameObject tile)
    {
        GameObject floor = tile.transform.GetChild(0).gameObject;
        GameObject[] walls = new GameObject[2] { tile.transform.GetChild(1).gameObject, tile.transform.GetChild(2).gameObject };
        GameObject[] horzontalSupports = new GameObject[2] { tile.transform.GetChild(3).gameObject, tile.transform.GetChild(4).gameObject };

        // Check for special cases to fix before destroying
        if (floor.name == "rope")
            RopeRemove(floor);
        if (floor.name == "ramp")
            RampRemove(floor);
        if (floor.name == "pole")
            PoleRemove(floor);
    }

    private void RopeRemove(GameObject rope)
    {

    }

    private void RampRemove(GameObject rope)
    {

    }

    private void PoleRemove(GameObject rope)
    {

    }

    private bool IsInRange(GameObject Object)
    {
        if ((rb.transform.position - Object.transform.position).magnitude < 3 * tileLength)
            return true;
        return false;
    }

    private void RenderProbes()
    {
        GameObject[] probesArray = probes.ToArray();
        for (int i = 0; i < probesArray.Length; i++)
        {
            if (probesArray[i] == null)
            {
                probes.Remove(probesArray[i]);
                continue;
            }
            if (IsInRange(probesArray[i]))
                probesArray[i].GetComponent<ReflectionProbe>().RenderProbe();
        }
    }
}