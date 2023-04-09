using UnityEngine;
using System.Collections;
using System.IO;
using System.Linq;
using System;
using System.Collections.Generic;

#region Points classes
[Serializable]
public class Point {
    public Vector3 position;
    public Color color;

    public Point(Vector3 position, Color color) {
        this.position = position;
        this.color = color;
    }
}

[Serializable]
public class Points {
    public Point[] points;
    public Points(Point[] points) {
        this.points = points;
    }
}

[Serializable]
public class PointV2 {
    public Vector3 position;

    public PointV2(Vector3 position) {
        this.position = position;
    }
}

[Serializable]
public class PointsV2 {
    public PointV2[] points;
    public PointsV2(PointV2[] points) {
        this.points = points;
    }
}

#endregion
public class PointCloudManager : MonoBehaviour {
    public event EventHandler OnCloudLoaded;

    // File
    [SerializeField] private string dataPath;
    [SerializeField] private string FileExtension;
    private string filename;
    [SerializeField] private Material matVertex;

    // GUI
    private float progress = 0;
    private string guiText;
    private bool loaded = false;

    // PointCloud
    private GameObject pointCloud;

    [SerializeField] private float scale = 1;
    [SerializeField] private bool invertYZ = false;
    [SerializeField] private bool forceReload = false;

    private int numPoints;
    private int numPointGroups;
    private int loadedPointGroups;
    private int limitPoints = 65000; //limite de pontos em cada filho
    private int maxPointsInOctreeElement = 10000;
    private List<GameObject> octreeElementsGameObjects;
    private Vector3[] points;
    private Color[] colors;

    // Leitura ply binário
    private int Lines;

    // Order points
    [SerializeField] private Vector3 minBounds;
    [SerializeField] private Vector3 maxBounds;

    private void Awake() {
        octreeElementsGameObjects = new List<GameObject>();
    }

    void Start() {
        //Debug.Log(BitConverter.IsLittleEndian);

        // Create Resources folder
        createFolders();

        // Get Filename
        filename = Path.GetFileName(dataPath) + "_";
        Debug.Log("FILENAME: " + filename);
        loadScene();
    }

    void loadScene() {
        // Check if the PointCloud was loaded previously
        if (!Directory.Exists(Application.dataPath + "/Resources/PointCloudMeshes/" + filename)) {
            UnityEditor.AssetDatabase.CreateFolder("Assets/Resources/PointCloudMeshes", filename);
            loadPointCloud();
        } else if (forceReload) {
            UnityEditor.FileUtil.DeleteFileOrDirectory(Application.dataPath + "/Resources/PointCloudMeshes/" + filename);
            UnityEditor.AssetDatabase.Refresh();
            UnityEditor.AssetDatabase.CreateFolder("Assets/Resources/PointCloudMeshes", filename);
            loadPointCloud();
        } else
            // Load stored PointCloud
            loadStoredMeshes();
    }
    void loadPointCloud() {
        //Check what file exists
        if (File.Exists(Application.dataPath + dataPath + FileExtension)) {
            // load off     
            StartCoroutine("loadOFF", dataPath + FileExtension);
        } else
            Debug.LogWarning("File '" + dataPath + "' could not be found");
    }

    // Load stored PointCloud
    void loadStoredMeshes() {
        Debug.Log("Using previously loaded PointCloud: " + filename);
        GameObject pointGroup = Instantiate(Resources.Load("PointCloudMeshes/" + filename)) as GameObject;
        OnFinishedLoading();
    }

    private void OnFinishedLoading() {
        loaded = true;
        Debug.Log("Finished loading");
        OnCloudLoaded?.Invoke(this, EventArgs.Empty);
    }

    // Start Coroutine of reading the points from the OFF file and creating the meshes
    IEnumerator loadOFF(string dPath) {

        // Read file
        if (FileExtension == ".ply") {
            Lines = 0;
            StreamReader sr = new StreamReader(Application.dataPath + dPath);
            sr.ReadLine(); // PLY
            Lines += 1;
            string format = sr.ReadLine();
            Lines += 1;
            string vertex = "";
            while (!vertex.StartsWith("element vertex")) {
                vertex = sr.ReadLine();
                Lines += 1;
            }
            numPoints = int.Parse(vertex.Split().Last());
            Debug.Log("Number of Points: " + numPoints);
            points = new Vector3[numPoints];
            colors = new Color[numPoints];
            // acha o fim do header
            while (sr.ReadLine() != "end_header") { Lines += 1; }
            sr.Close();
            Lines += 1;
            BinaryReader br = new BinaryReader(File.Open(Application.dataPath + dPath, FileMode.Open));
            // Lê as propriedades do arquivo binário
            // float = 4 bytes; uchar = 1 byte
            // 3 floats XYZ e 3 uchar RGB
            for (int i = 0; i < numPoints; i++) {
                while (Lines > 0) {
                    var leitura = br.ReadByte();
                    if (leitura == 10) {
                        Lines -= 1;
                    }
                }
                float x = br.ReadSingle() * scale;
                float y = br.ReadSingle() * scale;
                float z = br.ReadSingle() * scale;
                int R = br.ReadByte();
                int G = br.ReadByte();
                int B = br.ReadByte();
                if (!invertYZ) {
                    points[i] = new Vector3(x, y, z);
                } else {
                    points[i] = new Vector3(x, z, y);
                }
                colors[i] = new Color(R / 255.0f, G / 255.0f, B / 255.0f);

                // Relocate Points near the origin
                calculateMin(points[i]);

                // GUI
                progress = i * 1.0f / (numPoints - 1) * 1.0f;
                if (numPoints > 20) {
                    if (i % Mathf.FloorToInt(numPoints / 20) == 0) {
                        guiText = i.ToString() + " out of " + numPoints.ToString() + " loaded";
                        yield return null;
                    }
                }
            }

            pointCloud = new GameObject(filename);            
            // Create Octree
            yield return CreateOctree(pointCloud.transform, points.ToList(), minBounds, maxBounds);
            
            numPointGroups = octreeElementsGameObjects.Count;
            Debug.Log("Total Groups = " + numPointGroups);
            // Instantiate Point Groups
            loadedPointGroups = 0;

            //numPointGroups = Mathf.CeilToInt(numPoints * 1.0f / limitPoints * 1.0f);            

            for (int i = 0; i < numPointGroups; i++) {
                GameObject go = octreeElementsGameObjects[i];                
                MyInstantiateMesh(go, go.GetComponent<PointList>().points);
                if (i % 10 == 0) {
                    guiText = i.ToString() + " out of " + numPointGroups.ToString() + " PointGroups loaded";
                    yield return null;
                }
            }
            //InstantiateMesh(numPointGroups - 1, numPoints - (numPointGroups - 1) * limitPoints);

            //Store PointCloud        
            //UnityEditor.PrefabUtility.SaveAsPrefabAsset(pointCloud, "Assets/Resources/PointCloudMeshes/" + filename + ".prefab");
            OnFinishedLoading();
        }
    }

    private IEnumerator CreateOctree(Transform parent, List<Vector3> points, Vector3 minBounds, Vector3 maxBounds) {
        Vector3 delta = (maxBounds - minBounds) / 2;
        Vector3 center = delta + minBounds;
        List<Vector3> pointsBottomBackLeft = new List<Vector3>(); //point.y<=center.y && point.z<=center.z && point.x<=center.x
        List<Vector3> pointsBottomFrontLeft = new List<Vector3>(); //point.y<=center.y && point.z>center.z && point.x<=center.x
        List<Vector3> pointsBottomBackRight = new List<Vector3>(); //point.y<=center.y && point.z<=center.z && point.x>center.x
        List<Vector3> pointsBottomFrontRight = new List<Vector3>(); //point.y<=center.y && point.z>center.z && point.x>center.x
        List<Vector3> pointsTopBackLeft = new List<Vector3>(); //point.y>center.y && point.z<=center.z && point.x<=center.x
        List<Vector3> pointsTopFrontLeft = new List<Vector3>(); //point.y>center.y && point.z>center.z && point.x<=center.x
        List<Vector3> pointsTopBackRight = new List<Vector3>(); //point.y>center.y && point.z<=center.z && point.x>center.x
        List<Vector3> pointsTopFrontRight = new List<Vector3>(); //point.y>center.y && point.z>center.z && point.x>center.x
        // Create the 8 elements for this node
        foreach (Vector3 point in points) {
            // Check if the point is in the top side
            if (point.y > center.y) {
                // Check if the point is in the front side
                if (point.z > center.z) {
                    // Check if the point is in the right side
                    if (point.x > center.x) {
                        pointsTopFrontRight.Add(point);
                    } else { // left
                        pointsTopFrontLeft.Add(point);
                    }
                } else { // back
                    // Check if the point is in the right side
                    if (point.x > center.x) {
                        pointsTopBackRight.Add(point);
                    } else { // left
                        pointsTopBackLeft.Add(point);
                    }
                }
            } else { // bottom
                // Check if the point is in the front side
                if (point.z > center.z) {
                    // Check if the point is in the right side
                    if (point.x > center.x) {
                        pointsBottomFrontRight.Add(point);
                    } else { // left
                        pointsBottomFrontLeft.Add(point);
                    }
                } else { // back
                    // Check if the point is in the right side
                    if (point.x > center.x) {
                        pointsBottomBackRight.Add(point);
                    } else { // left
                        pointsBottomBackLeft.Add(point);
                    }
                }
            }
        }

        List<OctreeElement> octreeElements = new List<OctreeElement>();
        octreeElements.Add(new OctreeElement(minBounds, center, pointsBottomBackLeft));
        octreeElements.Add(new OctreeElement(new Vector3(minBounds.x, minBounds.y, center.z), new Vector3(center.x, center.y, maxBounds.z), pointsBottomFrontLeft));
        octreeElements.Add(new OctreeElement(new Vector3(center.x, minBounds.y, minBounds.z), new Vector3(maxBounds.x, center.y, center.z), pointsBottomBackRight));
        octreeElements.Add(new OctreeElement(new Vector3(center.x, minBounds.y, center.z), new Vector3(maxBounds.x, center.y, maxBounds.z), pointsBottomFrontRight));
        octreeElements.Add(new OctreeElement(new Vector3(minBounds.x, center.y, minBounds.z), new Vector3(center.x, maxBounds.y, center.z), pointsTopBackLeft));
        octreeElements.Add(new OctreeElement(new Vector3(minBounds.x, center.y, center.z), new Vector3(center.x, maxBounds.y, maxBounds.z), pointsTopFrontLeft));
        octreeElements.Add(new OctreeElement(new Vector3(center.x, center.y, minBounds.z), new Vector3(maxBounds.x, maxBounds.y, center.z), pointsTopBackRight));
        octreeElements.Add(new OctreeElement(center, maxBounds, pointsTopFrontRight));
        for (int i = 0; i < octreeElements.Count; i++) {
            OctreeElement octreeElement = octreeElements[i];
            if (octreeElement.points.Count > 0) {
                GameObject octreeElementGO = new GameObject($"{parent.name}{i}");
                octreeElementGO.transform.parent = parent;
                if (octreeElement.points.Count > maxPointsInOctreeElement) {
                    yield return CreateOctree(octreeElementGO.transform, octreeElement.points, octreeElement.minBounds, octreeElement.maxBounds);
                } else {
                    PointList pList = octreeElementGO.AddComponent<PointList>();
                    pList.points = octreeElement.points;
                    pList.maxBounds = octreeElement.maxBounds;
                    pList.minBounds = octreeElement.minBounds;
                    octreeElementsGameObjects.Add(octreeElementGO);
                }
            }
        }
        yield return null;
    }

    public class OctreeElement {
        public Vector3 minBounds;
        public Vector3 maxBounds;
        public List<Vector3> points;
        public OctreeElement(Vector3 minBounds, Vector3 maxBounds, List<Vector3> points) {
            this.minBounds = minBounds; this.maxBounds = maxBounds; this.points = points;
        }
    }

    private void MyInstantiateMesh(GameObject pointGroup, List<Vector3> points) {
        pointGroup.AddComponent<MeshFilter>();
        pointGroup.AddComponent<MeshRenderer>();
        pointGroup.GetComponent<Renderer>().material = matVertex;
        pointGroup.GetComponent<MeshFilter>().mesh = MyCreateMesh(meshInd, nPoints, limitPoints);
        pointGroup.isStatic = true;
    }

    void InstantiateMesh(int meshInd, int nPoints) {
        // Create Mesh
        GameObject pointGroup = new GameObject(filename + "_" + meshInd);
        pointGroup.AddComponent<MeshFilter>();
        pointGroup.AddComponent<MeshRenderer>();
        pointGroup.GetComponent<Renderer>().material = matVertex;
        pointGroup.GetComponent<MeshFilter>().mesh = CreateMesh(meshInd, nPoints, limitPoints);
        pointGroup.transform.parent = pointCloud.transform;
        pointGroup.isStatic = true;
        // Store Mesh
        UnityEditor.AssetDatabase.CreateAsset(pointGroup.GetComponent<MeshFilter>().mesh, "Assets/Resources/PointCloudMeshes/" + filename + @"/" + filename + meshInd + ".asset");
        UnityEditor.AssetDatabase.SaveAssets();
        UnityEditor.AssetDatabase.Refresh();
    }

    Mesh MyCreateMesh(int id, int nPoints, int limitPoints) {
        Mesh mesh = new Mesh();
        Vector3[] myPoints = new Vector3[nPoints];
        int[] indices = new int[nPoints];
        Color[] myColors = new Color[nPoints];
        for (int i = 0; i < nPoints; ++i) {
            myPoints[i] = points[id * limitPoints + i] - minBounds;
            indices[i] = i;
            myColors[i] = colors[id * limitPoints + i];
        }
        mesh.vertices = myPoints;
        mesh.colors = myColors;
        mesh.SetIndices(indices, MeshTopology.Points, 0);

        mesh.uv = new Vector2[nPoints];
        mesh.normals = new Vector3[nPoints];
        return mesh;
    }

    Mesh CreateMesh(int id, int nPoints, int limitPoints) {
        Mesh mesh = new Mesh();
        Vector3[] myPoints = new Vector3[nPoints];
        int[] indices = new int[nPoints];
        Color[] myColors = new Color[nPoints];
        for (int i = 0; i < nPoints; ++i) {
            myPoints[i] = points[id * limitPoints + i] - minBounds;
            indices[i] = i;
            myColors[i] = colors[id * limitPoints + i];
        }
        mesh.vertices = myPoints;
        mesh.colors = myColors;
        mesh.SetIndices(indices, MeshTopology.Points, 0);

        mesh.uv = new Vector2[nPoints];
        mesh.normals = new Vector3[nPoints];
        return mesh;
    }

    #region MyRegion
    //private void CreateMeshV2(GameObject obj, int id, int nPoints, int limitPoints)
    //{
    //    Mesh mesh = new Mesh();
    //    mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
    //    Vector3[] myPoints = new Vector3[nPoints];
    //    int[] indecies = new int[nPoints];
    //    Color[] myColors = new Color[nPoints];
    //    for (int i = 0; i < nPoints; ++i)
    //    {
    //        myPoints[i] = points[id * limitPoints + i] - minValue;
    //        indecies[i] = i;
    //        myColors[i] = colors[id * limitPoints + i];
    //    }
    //    UpdateQuads(obj, myPoints, myColors);
    //}

    //private void UpdateQuads(GameObject obj, Vector3[] vertices, Color[] colors)
    //{
    //    for (int i = 0; i < vertices.Length; i++)
    //    {
    //        GameObject quad = _quads[i];
    //        quad.transform.parent = obj.transform;
    //        Color color = colors[i];
    //        NewQuad(obj, quad, vertices[i], color);
    //    }
    //    CombineMeshesV2(obj, vertices);
    //}

    //private void NewQuad(GameObject parent, GameObject quad, Vector3 pos, Color color)
    //{
    //    quad.transform.parent = parent.transform;
    //    quad.transform.localPosition = pos;
    //    Color32[] colors = new Color32[quad.GetComponent<MeshFilter>().mesh.vertices.Length];
    //    for (int j = 0; j < colors.Length; j++)
    //    {
    //        colors[j] = color;
    //    }
    //    quad.GetComponent<MeshFilter>().mesh.colors32 = colors;
    //}

    //private void CombineMeshesV2(GameObject obj, Vector3[] vertices)
    //{
    //    //Temporarily set position to zero to make matrix math easier
    //    Vector3 position = obj.transform.position;
    //    obj.transform.position = Vector3.zero;

    //    //Get all mesh filters and combine
    //    MeshFilter[] meshFilters = obj.GetComponentsInChildren<MeshFilter>();
    //    CombineInstance[] combine = new CombineInstance[meshFilters.Length];
    //    int i = 1;
    //    while (i < meshFilters.Length)
    //    {
    //        combine[i].mesh = meshFilters[i].sharedMesh;
    //        combine[i].transform = meshFilters[i].transform.localToWorldMatrix;
    //        //meshFilters[i].gameObject.SetActive(false);
    //        //Destroy(meshFilters[i].gameObject);
    //        i++;
    //    }

    //    Mesh mesh = new Mesh();
    //    mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
    //    obj.transform.GetComponent<MeshFilter>().mesh = mesh;
    //    obj.transform.GetComponent<MeshFilter>().mesh.CombineMeshes(combine, true, true);
    //    obj.transform.gameObject.SetActive(true);

    //    //Return to original position
    //    obj.transform.position = position;

    //    //Add collider to mesh (if needed)
    //    obj.AddComponent<BoxCollider>();
    //    obj.GetComponent<BoxCollider>().isTrigger = true;
    //}
    #endregion

    void calculateMin(Vector3 point) {
        if (minBounds.magnitude == 0) {
            minBounds = point;
            maxBounds = point;
        }
        if (point.x < minBounds.x) minBounds.x = point.x;
        if (point.y < minBounds.y) minBounds.y = point.y;
        if (point.z < minBounds.z) minBounds.z = point.z;
        if (point.x > maxBounds.x) maxBounds.x = point.x;
        if (point.y > maxBounds.y) maxBounds.y = point.y;
        if (point.z > maxBounds.z) maxBounds.z = point.z;
    }

    void createFolders() {
        if (!Directory.Exists(Application.dataPath + "/Resources/"))
            UnityEditor.AssetDatabase.CreateFolder("Assets", "Resources");
        if (!Directory.Exists(Application.dataPath + "/Resources/PointCloudMeshes/"))
            UnityEditor.AssetDatabase.CreateFolder("Assets/Resources", "PointCloudMeshes");
    }

    void OnGUI() {
        if (!loaded) {
            GUI.BeginGroup(new Rect(Screen.width / 2 - 100, Screen.height / 2, 400.0f, 20));
            GUI.Box(new Rect(0, 0, 300.0f, 20.0f), guiText);
            GUI.Box(new Rect(0, 0, progress * 300.0f, 20), "");
            GUI.EndGroup();
        }
    }
}
