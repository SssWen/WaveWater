using UnityEngine;
using System.Collections;
using UnityEngine.UI;

/// <summary>
/// 液体模拟器
/// </summary>
public class LiquidSimulator : MonoBehaviour
{
    #region public
    /// <summary>
    /// 网格单元格大小
    /// </summary>
    public float geometryCellSize;
    /// <summary>
    /// 液面宽度
    /// </summary>
    public float liquidWidth;
    /// <summary>
    /// 液面长度
    /// </summary>
    public float liquidLength;
    /// <summary>
    /// 液体深度
    /// </summary>
    public float liquidDepth;
    /// <summary>
    /// 用于计算d = 1/heightMapSize,相邻顶点在x和y方向的间距
    /// </summary>
    public int heightMapSize;

    public Texture2D mask;

    /// <summary>
    /// 粘度系数
    /// </summary>
    public float Viscosity
    {
        get { return m_Viscosity; }
    }

    /// <summary>
    /// 波速
    /// </summary>
    public float Velocity
    {
        get { return m_Velocity; }
    }

    /// <summary>
    /// 力度系数
    /// </summary>
    public float ForceFactor
    {
        get { return m_ForceFactor; }
    }
    
    #endregion

    [SerializeField] private float m_Viscosity;
    [SerializeField] private float m_Velocity;
    [SerializeField] private float m_ForceFactor;
    [SerializeField] private Material m_LiquidMaterial;

    private bool m_IsSupported;
    
    private Mesh m_LiquidMesh;
    private MeshFilter m_LiquidMeshFilter;
    private MeshRenderer m_LiquidMeshRenderer;
    private LiquidSampleCamera m_SampleCamera;
    //private ReflectCamera m_ReflectCamera;

    private Vector4 m_LiquidParams;

    private float m_SampleSpacing;

    private Vector4 m_LiquidArea;

    private static LiquidSimulator Instance
    {
        get
        {
            if (sInstance == null)
                sInstance = FindObjectOfType<LiquidSimulator>();
            return sInstance;
        }
    }

    private static LiquidSimulator sInstance;

    void Start()
    {
        m_SampleSpacing = 1.0f / heightMapSize;

        m_IsSupported = CheckSupport();
        if (!m_IsSupported)
            return;

        m_SampleCamera = new GameObject("[LiquidSampleCamera]").AddComponent<LiquidSampleCamera>();
        m_SampleCamera.transform.SetParent(transform);
        m_SampleCamera.transform.localPosition = Vector3.zero;
        m_SampleCamera.transform.localEulerAngles = new Vector3(90,0,0);
        m_SampleCamera.Init(liquidWidth, liquidLength, liquidDepth, m_ForceFactor,
            new Vector4(transform.up.x, transform.up.y, transform.up.z,
                -Vector3.Dot(transform.up, transform.position)), m_LiquidParams, heightMapSize, mask);


        m_LiquidMeshRenderer = gameObject.GetComponent<MeshRenderer>();
        if (m_LiquidMeshRenderer == null)
            m_LiquidMeshRenderer = gameObject.AddComponent<MeshRenderer>();
        m_LiquidMeshFilter = gameObject.GetComponent<MeshFilter>();
        if (m_LiquidMeshFilter == null)
            m_LiquidMeshFilter = gameObject.AddComponent<MeshFilter>();

        m_LiquidMesh = Utils.GenerateLiquidMesh(liquidWidth, liquidLength, geometryCellSize);
        m_LiquidMeshFilter.sharedMesh = m_LiquidMesh;
        m_LiquidMeshRenderer.sharedMaterial = m_LiquidMaterial;

        m_LiquidArea = new Vector4(transform.position.x - liquidWidth * 0.5f,
            transform.position.z - liquidLength * 0.5f,
            transform.position.x + liquidWidth * 0.5f, transform.position.z + liquidLength * 0.5f);
        
       
    }

    public static void DrawObject(Renderer renderer)
    {
        if (Instance != null)
        {
            Instance.m_SampleCamera.DrawRenderer(renderer); 
        }
    }

    public static void DrawMesh(Mesh mesh, Matrix4x4 matrix)
    {
        if (Instance != null)
        {
            Instance.m_SampleCamera.ForceDrawMesh(mesh, matrix);            
        }
    }

    void OnWillRenderObject()
    {
        Shader.SetGlobalVector("_LiquidArea", m_LiquidArea);      
    }

    bool CheckSupport()
    {
        if (geometryCellSize <= 0)
        {
            Debug.LogError("网格单元格大小不允许小于等于0！");
            return false;
        }
        if (liquidWidth <= 0 || liquidLength <= 0)
        {
            Debug.LogError("液体长宽不允许小于等于0！");
            return false;
        }
        if (liquidDepth <= 0)
        {
            Debug.LogError("液体深度不允许小于等于0！");
            return false;
        }


        if (!RefreshLiquidParams(m_Velocity, m_Viscosity))
            return false;

        return true;
    }

    /// <summary>
    /// 计算顶点的下一个顶点，所需的 含粘滞阻尼的二维波动方程 参数，
    /// 计算公式详见 《3D游戏与计算机图形学中的数学方法第三版》15.1.3 287页计算表面位移相关表达式
    /// </summary>
    /// <param name="speed"></param>
    /// <param name="viscosity"> 黏稠度 </param>
    /// <returns></returns>
    private bool RefreshLiquidParams(float speed, float viscosity)
    {
        if (speed <= 0)
        {
            Debug.LogError("波速不允许小于等于0！");
            return false;
        }
        if (viscosity <= 0)
        {
            Debug.LogError("粘度系数不允许小于等于0！");
            return false;
        }
        float maxvelocity = m_SampleSpacing / (2 * Time.fixedDeltaTime) * Mathf.Sqrt(viscosity * Time.fixedDeltaTime + 2);
        float velocity = maxvelocity * speed;
        float viscositySq = viscosity * viscosity;
        float velocitySq = velocity * velocity; // c*c
        float deltaSizeSq = m_SampleSpacing * m_SampleSpacing; // d*d
        float dt = Mathf.Sqrt(viscositySq + 32 * velocitySq / (deltaSizeSq));
        float dtden = 8 * velocitySq / (deltaSizeSq);
        float maxT = (viscosity + dt) / dtden;
        float maxT2 = (viscosity - dt) / dtden;
        if (maxT2 > 0 && maxT2 < maxT)
            maxT = maxT2;
        if (maxT < Time.fixedDeltaTime)
        {
            Debug.LogError("粘度系数不符合要求");
            return false;
        }

        // 8*c^2*t^2/(d^2)
        float fac = velocitySq * Time.fixedDeltaTime * Time.fixedDeltaTime / deltaSizeSq;
        float i = viscosity * Time.fixedDeltaTime - 2;
        float j = viscosity * Time.fixedDeltaTime + 2;

        float k1 = (4 - 8 * fac) / (j);
        float k2 = i / j;
        float k3 = 2 * fac / j;

        m_LiquidParams = new Vector4(k1, k2, k3, m_SampleSpacing);
        
        m_Velocity = speed;
        m_Viscosity = viscosity;

        return true;
    }

    void OnDrawGizmosSelected()
    {
        Utils.DrawWireCube(transform.position, transform.eulerAngles.y, liquidWidth, liquidLength, -liquidDepth, 0, Color.green);
    }
}
