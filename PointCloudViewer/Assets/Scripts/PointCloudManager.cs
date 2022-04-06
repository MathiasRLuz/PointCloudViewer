using UnityEngine;
using System.Collections;
using System.IO;
using System.Linq;

public class PointCloudManager : MonoBehaviour {

	// File
	public string dataPath;
	public string FileExtension;
	private string filename;
	public Material matVertex;

	// GUI
	private float progress = 0;
	private string guiText;
	public bool loaded = false;

	// PointCloud
	public GameObject pointCloud;

	public float scale = 1;
	public bool invertYZ = false;
	public bool forceReload = false;

	public int numPoints;
	public int numPointGroups;
	private int limitPoints = 65000; //limite de pontos em cada filho 
	private int limitSubPoints = 1000; //limite de pontos em cada filho dos filhos 

	private Vector3[] points;
	private Color[] colors;
	private Vector3 minValue;

	void Start () {
		// Create Resources folder
		createFolders ();

		// Get Filename
		filename = Path.GetFileName(dataPath);

		loadScene ();
	}

	void loadScene(){
		// Check if the PointCloud was loaded previously
		if(!Directory.Exists (Application.dataPath + "/Resources/PointCloudMeshes/" + filename)){
			UnityEditor.AssetDatabase.CreateFolder ("Assets/Resources/PointCloudMeshes", filename);
			loadPointCloud ();
		} else if (forceReload){
			UnityEditor.FileUtil.DeleteFileOrDirectory(Application.dataPath + "/Resources/PointCloudMeshes/" + filename);
			UnityEditor.AssetDatabase.Refresh();
			UnityEditor.AssetDatabase.CreateFolder ("Assets/Resources/PointCloudMeshes", filename);
			loadPointCloud ();
		} else
			// Load stored PointCloud
			loadStoredMeshes();
	}
	void loadPointCloud(){
		// Check what file exists
		if (File.Exists(Application.dataPath + dataPath + FileExtension))
		{
			// load off			
			StartCoroutine("loadOFF", dataPath + FileExtension);
		}			
		else			
			Debug.LogWarning("File '" + dataPath + "' could not be found"); 		
	}
	
	// Load stored PointCloud
	void loadStoredMeshes(){
		Debug.Log("Using previously loaded PointCloud: " + filename);
		GameObject pointGroup = Instantiate(Resources.Load ("PointCloudMeshes/" + filename)) as GameObject;
		loaded = true;
	}
	
	// Start Coroutine of reading the points from the OFF file and creating the meshes
	IEnumerator loadOFF(string dPath){

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
			loaded = true;
		}
		else if (FileExtension == ".ply")
        {
			StreamReader sr = new StreamReader(Application.dataPath + dPath);
			sr.ReadLine(); // PLY
			string format = sr.ReadLine();
			string vertex = sr.ReadLine();
			numPoints = int.Parse(vertex.Split().Last());
			// acha o fim do header
			while (sr.ReadLine() != "end_header"){}
			sr.Close();
			BinaryReader br = new BinaryReader(File.Open(Application.dataPath + dPath,FileMode.Open));			
			Debug.Log(br.ReadSingle());
        }	
	}
	
	void InstantiateMesh(int meshInd, int nPoints){
		// Create Mesh
		GameObject pointGroup = new GameObject (filename + meshInd);
		pointGroup.AddComponent<MeshFilter> ();
		pointGroup.AddComponent<MeshRenderer> ();
		pointGroup.GetComponent<Renderer>().material = matVertex;
		pointGroup.GetComponent<MeshFilter> ().mesh = CreateMesh (meshInd, nPoints, limitPoints);
		pointGroup.transform.parent = pointCloud.transform;

		// Store Mesh
		UnityEditor.AssetDatabase.CreateAsset(pointGroup.GetComponent<MeshFilter> ().mesh, "Assets/Resources/PointCloudMeshes/" + filename + @"/" + filename + meshInd + ".asset");
		UnityEditor.AssetDatabase.SaveAssets ();
		UnityEditor.AssetDatabase.Refresh();
	}

	Mesh CreateMesh(int id, int nPoints, int limitPoints){		
		Mesh mesh = new Mesh ();		
		Vector3[] myPoints = new Vector3[nPoints]; 
		int[] indecies = new int[nPoints];
		Color[] myColors = new Color[nPoints];
		for(int i=0;i<nPoints;++i) {
			myPoints[i] = points[id*limitPoints + i] - minValue;
			indecies[i] = i;
			myColors[i] = colors[id*limitPoints + i];
		}		
		mesh.vertices = myPoints;
		mesh.colors = myColors;
		mesh.SetIndices(indecies, MeshTopology.Points,0);
		mesh.uv = new Vector2[nPoints];
		mesh.normals = new Vector3[nPoints];
		return mesh;
	}

	void calculateMin(Vector3 point){
		if (minValue.magnitude == 0)
			minValue = point;
		if (point.x < minValue.x)
			minValue.x = point.x;
		if (point.y < minValue.y)
			minValue.y = point.y;
		if (point.z < minValue.z)
			minValue.z = point.z;
	}

	void createFolders(){
		if(!Directory.Exists (Application.dataPath + "/Resources/"))
			UnityEditor.AssetDatabase.CreateFolder ("Assets", "Resources");
		if (!Directory.Exists (Application.dataPath + "/Resources/PointCloudMeshes/"))
			UnityEditor.AssetDatabase.CreateFolder ("Assets/Resources", "PointCloudMeshes");
	}

	void OnGUI(){
		if (!loaded){
			GUI.BeginGroup (new Rect(Screen.width/2-100, Screen.height/2, 400.0f, 20));
			GUI.Box (new Rect (0, 0, 300.0f, 20.0f), guiText);
			GUI.Box (new Rect (0, 0, progress*300.0f, 20), "");
			GUI.EndGroup ();
		}
	}
}
