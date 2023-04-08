using UnityEngine;
using System.Collections;
using System.IO;
using System.Linq;
using System;
using UnityEngine.Pool;
using System.Collections.Generic;
using UnityEngine.Networking;

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
public class PointCloudManager : MonoBehaviour
{
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

    [SerializeField] private int numPoints;
    [SerializeField] private int numPointGroups;
    private int limitPoints = 65000; //limite de pontos em cada filho
    private Vector3[] points;
    private Color[] colors;
    private Vector3 minValue;
    [SerializeField] private Vector3[] firstPoints;

    // Leitura ply binário
    private int Lines;

    // Pool
    //public ObjectPool<GameObject> MyPool;
    //[SerializeField] private GameObject _quadPrefab;
    //[SerializeField] private int _spawnAmount = 20;
    //private GameObject[] _quads;

    // Order points
    [SerializeField] private Vector3 minBounds;
    [SerializeField] private Vector3 maxBounds;

    void Start()
    {
        //_quads = new GameObject[_spawnAmount];
        //for (int i = 0; i < _spawnAmount; i++)
        //{
        //    GameObject quad = Instantiate(_quadPrefab, transform);
        //    _quads[i] = quad;
        //}
        //Debug.Log(BitConverter.IsLittleEndian);

        // Create Resources folder
        createFolders();

        // Get Filename
        filename = Path.GetFileName(dataPath);
        Debug.Log("FILENAME: " + filename);
        loadScene();
    }

    void loadScene()
    {
        // Check if the PointCloud was loaded previously
        if (!Directory.Exists(Application.dataPath + "/Resources/PointCloudMeshes/" + filename))
        {
            UnityEditor.AssetDatabase.CreateFolder("Assets/Resources/PointCloudMeshes", filename);
            loadPointCloud();
        }
        else if (forceReload)
        {
            UnityEditor.FileUtil.DeleteFileOrDirectory(Application.dataPath + "/Resources/PointCloudMeshes/" + filename);
            UnityEditor.AssetDatabase.Refresh();
            UnityEditor.AssetDatabase.CreateFolder("Assets/Resources/PointCloudMeshes", filename);
            loadPointCloud();
        }
        else
            // Load stored PointCloud
            loadStoredMeshes();
    }
    void loadPointCloud()
    {
        //Check what file exists
        if (File.Exists(Application.dataPath + dataPath + FileExtension))
        {
            // load off     
            StartCoroutine("loadOFF", dataPath + FileExtension);
        }
        else
            Debug.LogWarning("File '" + dataPath + "' could not be found");
    }

    // Load stored PointCloud
    void loadStoredMeshes()
    {
        Debug.Log("Using previously loaded PointCloud: " + filename);
        GameObject pointGroup = Instantiate(Resources.Load("PointCloudMeshes/" + filename)) as GameObject;
        OnFinishedLoading();
    }

    private void OnFinishedLoading() {
        loaded = true;
        OnCloudLoaded?.Invoke(this, EventArgs.Empty);
    }

    // Start Coroutine of reading the points from the OFF file and creating the meshes
    IEnumerator loadOFF(string dPath)
    {

        // Read file

        if (FileExtension == ".off")
        {
            StreamReader sr = new StreamReader(Application.dataPath + dPath);
            sr.ReadLine(); // OFF
            string[] buffer = sr.ReadLine().Split(); // nPoints, nFaces   
            numPoints = int.Parse(buffer[0]);
            Debug.Log("Number of Points: " + numPoints);
            points = new Vector3[numPoints];
            colors = new Color[numPoints];
            minValue = new Vector3();

            for (int i = 0; i < numPoints; i++)
            {
                buffer = sr.ReadLine().Split();

                if (!invertYZ)
                {
                    points[i] = new Vector3(float.Parse(buffer[0], System.Globalization.CultureInfo.InvariantCulture) * scale, float.Parse(buffer[1], System.Globalization.CultureInfo.InvariantCulture) * scale, float.Parse(buffer[2], System.Globalization.CultureInfo.InvariantCulture) * scale);
                }
                else
                {
                    points[i] = new Vector3(float.Parse(buffer[0], System.Globalization.CultureInfo.InvariantCulture) * scale, float.Parse(buffer[2], System.Globalization.CultureInfo.InvariantCulture) * scale, float.Parse(buffer[1], System.Globalization.CultureInfo.InvariantCulture) * scale);
                }

                if (buffer.Length >= 5)
                    colors[i] = new Color(int.Parse(buffer[3]) / 255.0f, int.Parse(buffer[4]) / 255.0f, int.Parse(buffer[5]) / 255.0f);
                else
                    colors[i] = Color.cyan;

                // Relocate Points near the origin
                calculateMin(points[i]);

                // GUI
                progress = i * 1.0f / (numPoints - 1) * 1.0f;
                if (numPoints > 20)
                {
                    if (i % Mathf.FloorToInt(numPoints / 20) == 0)
                    {
                        guiText = i.ToString() + " out of " + numPoints.ToString() + " loaded";
                        yield return null;
                    }
                }
            }
            // Reordenar os (limitPoints) pontos pela coordenada Z para criar os grupos menores

            firstPoints = new Vector3[25];
            for (int i = 0; i < firstPoints.Length; i++)
            {
                firstPoints[i] = points[i];
            }

            // Instantiate Point Groups
            numPointGroups = Mathf.CeilToInt(numPoints * 1.0f / limitPoints * 1.0f);
            pointCloud = new GameObject(filename);

            for (int i = 0; i < numPointGroups - 1; i++)
            {
                InstantiateMesh(i, limitPoints);
                if (i % 10 == 0)
                {
                    guiText = i.ToString() + " out of " + numPointGroups.ToString() + " PointGroups loaded";
                    yield return null;
                }
            }
            InstantiateMesh(numPointGroups - 1, numPoints - (numPointGroups - 1) * limitPoints);

            //Store PointCloud        
            UnityEditor.PrefabUtility.SaveAsPrefabAsset(pointCloud, "Assets/Resources/PointCloudMeshes/" + filename + ".prefab");
            OnFinishedLoading();
        }
        else if (FileExtension == ".ply")
        {
            Lines = 0;
            Debug.Log("PLY");
            StreamReader sr = new StreamReader(Application.dataPath + dPath);
            sr.ReadLine(); // PLY
            Lines += 1;
            string format = sr.ReadLine();
            Lines += 1;
            string vertex = "";
            while (!vertex.StartsWith("element vertex"))
            {
                vertex = sr.ReadLine();
                Lines += 1;
            }
            numPoints = int.Parse(vertex.Split().Last());
            Debug.Log("Number of Points: " + numPoints);
            points = new Vector3[numPoints];
            colors = new Color[numPoints];
            minValue = new Vector3();
            // acha o fim do header
            while (sr.ReadLine() != "end_header") { Lines += 1; }
            sr.Close();
            Lines += 1;
            BinaryReader br = new BinaryReader(File.Open(Application.dataPath + dPath, FileMode.Open));
            // Lê as propriedades do arquivo binário
            // float = 4 bytes; uchar = 1 byte
            // 3 floats XYZ e 3 uchar RGB
            for (int i = 0; i < numPoints; i++)
            {
                while (Lines > 0)
                {
                    var leitura = br.ReadByte();
                    if (leitura == 10)
                    {
                        Lines -= 1;
                    }
                }
                float x = br.ReadSingle() * scale;
                float y = br.ReadSingle() * scale;
                float z = br.ReadSingle() * scale;
                int R = br.ReadByte();
                int G = br.ReadByte();
                int B = br.ReadByte();
                if (!invertYZ)
                {
                    points[i] = new Vector3(x, y, z);
                }
                else
                {
                    points[i] = new Vector3(x, z, y);
                }
                colors[i] = new Color(R / 255.0f, G / 255.0f, B / 255.0f);

                // Relocate Points near the origin
                calculateMin(points[i]);

                // GUI
                progress = i * 1.0f / (numPoints - 1) * 1.0f;
                if (numPoints > 20)
                {
                    if (i % Mathf.FloorToInt(numPoints / 20) == 0)
                    {
                        guiText = i.ToString() + " out of " + numPoints.ToString() + " loaded";
                        yield return null;
                    }
                }
            }

            firstPoints = new Vector3[25];
            for (int i = 0; i < firstPoints.Length; i++)
            {
                firstPoints[i] = points[i];
            }

            // Instantiate Point Groups
            numPointGroups = Mathf.CeilToInt(numPoints * 1.0f / limitPoints * 1.0f);
            pointCloud = new GameObject(filename);

            for (int i = 0; i < numPointGroups - 1; i++)
            {
                InstantiateMesh(i, limitPoints);
                if (i % 10 == 0)
                {
                    guiText = i.ToString() + " out of " + numPointGroups.ToString() + " PointGroups loaded";
                    yield return null;
                }
            }
            InstantiateMesh(numPointGroups - 1, numPoints - (numPointGroups - 1) * limitPoints);

            //Store PointCloud        
            UnityEditor.PrefabUtility.SaveAsPrefabAsset(pointCloud, "Assets/Resources/PointCloudMeshes/" + filename + ".prefab");


            //Tentativa de salvar as posições dos pontos em um json para aplicar clustering antes de criar as meshes (USA MUITA MEMÓRIA)
            //PointV2[] PointArray = new PointV2[1000000];
            //         for (int i = 0; i < PointArray.Length; i++)
            //         {
            //  PointV2 pointInfo = new PointV2(points[i]);
            //  PointArray[i] = pointInfo;
            //         }
            //PointsV2 MyPoints = new PointsV2(PointArray);

            //string outputString = JsonUtility.ToJson(MyPoints);
            ////Debug.Log(outputString);
            //File.WriteAllText("MyFile.json", outputString);
            OnFinishedLoading();
        }
    }

    void InstantiateMesh(int meshInd, int nPoints)
    {
        // Create Mesh
        GameObject pointGroup = new GameObject(filename + meshInd);
        pointGroup.AddComponent<MeshFilter>();
        pointGroup.AddComponent<MeshRenderer>();
        pointGroup.GetComponent<Renderer>().material = matVertex;
        pointGroup.GetComponent<MeshFilter>().mesh = CreateMesh(meshInd, nPoints, limitPoints);
        //CreateMeshV2(pointGroup, meshInd, nPoints, limitPoints);
        pointGroup.transform.parent = pointCloud.transform;
        pointGroup.isStatic = true;
        // Store Mesh
        UnityEditor.AssetDatabase.CreateAsset(pointGroup.GetComponent<MeshFilter>().mesh, "Assets/Resources/PointCloudMeshes/" + filename + @"/" + filename + meshInd + ".asset");
        UnityEditor.AssetDatabase.SaveAssets();
        UnityEditor.AssetDatabase.Refresh();
    }

    Mesh CreateMesh(int id, int nPoints, int limitPoints)
    {
        Mesh mesh = new Mesh();
        Vector3[] myPoints = new Vector3[nPoints];
        int[] indecies = new int[nPoints];
        Color[] myColors = new Color[nPoints];
        for (int i = 0; i < nPoints; ++i)
        {
            myPoints[i] = points[id * limitPoints + i] - minValue;
            indecies[i] = i;
            myColors[i] = colors[id * limitPoints + i];
        }
        mesh.vertices = myPoints;
        mesh.colors = myColors;
        mesh.SetIndices(indecies, MeshTopology.Points, 0);

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


    void calculateMin(Vector3 point)
    {
        if (minValue.magnitude == 0)
        {
            minValue = point;
            minBounds = point;
            maxBounds = point;
        }
        if (point.x < minValue.x)
        {
            minValue.x = point.x;
            minBounds.x = point.x;
        }
        if (point.y < minValue.y)
        {
            minValue.y = point.y;
            minBounds.y = point.y;
        }
        if (point.z < minValue.z)
        {
            minValue.z = point.z;
            minBounds.z = point.z;
        }
        if (point.x > maxBounds.x) maxBounds.x = point.x;
        if (point.y > maxBounds.y) maxBounds.y = point.y;
        if (point.z > maxBounds.z) maxBounds.z = point.z;
    }

    void createFolders()
    {
        if (!Directory.Exists(Application.dataPath + "/Resources/"))
            UnityEditor.AssetDatabase.CreateFolder("Assets", "Resources");
        if (!Directory.Exists(Application.dataPath + "/Resources/PointCloudMeshes/"))
            UnityEditor.AssetDatabase.CreateFolder("Assets/Resources", "PointCloudMeshes");
    }

    void OnGUI()
    {
        if (!loaded)
        {
            GUI.BeginGroup(new Rect(Screen.width / 2 - 100, Screen.height / 2, 400.0f, 20));
            GUI.Box(new Rect(0, 0, 300.0f, 20.0f), guiText);
            GUI.Box(new Rect(0, 0, progress * 300.0f, 20), "");
            GUI.EndGroup();
        }
    }
}
