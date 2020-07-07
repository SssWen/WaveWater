using UnityEngine;
using UnityEngine.Rendering;
using System.Collections;

/// <summary>
/// 液体高度采样相机,详细计算公式参考<3D游戏与计算机图形学中的数学方法>第15章 流体与织物仿真
/// 高度图/法线贴图/波动方程计算水面波动
/// </summary>
public class LiquidSampleCamera : MonoBehaviour
{
    private Camera m_Camera;

    private RenderTexture m_CurTexture;
    private RenderTexture m_PreTexture;
    private RenderTexture m_HeightMap;
    private RenderTexture m_NormalMap;

    private Material m_WaveEquationMat;
    private Material m_NormalGenerateMat;

    private Vector4 m_WaveParams;

    private CommandBuffer m_CommandBuffer;
    private Material m_ForceMaterial;

    // 算出物体浸入水中的深度，保存在当前Camera下m_CurTexture
    public void DrawRenderer(Renderer renderer)
    {
        if (!renderer)
            return;        
        m_CommandBuffer.DrawRenderer(renderer, m_ForceMaterial);
    }

    public void ForceDrawMesh(Mesh mesh, Matrix4x4 matrix)
    {
        if (!mesh)
            return;        
        m_CommandBuffer.DrawMesh(mesh, matrix, m_ForceMaterial);
    }

    public void Init(float width, float height, float depth, float force, Vector4 plane, Vector4 waveParams,
        int texSize, Texture2D mask)
    {
        m_WaveParams = waveParams;

        m_Camera = gameObject.AddComponent<Camera>();
        m_Camera.aspect = width / height;
        m_Camera.backgroundColor = Color.black;
        m_Camera.cullingMask = 0;
        m_Camera.depth = 0;
        m_Camera.farClipPlane = depth;
        m_Camera.nearClipPlane = 0;
        m_Camera.orthographic = true;
        m_Camera.orthographicSize = height * 0.5f;
        m_Camera.clearFlags = CameraClearFlags.Depth;
        m_Camera.allowHDR = false;

        m_CommandBuffer = new CommandBuffer();
        m_Camera.AddCommandBuffer(CameraEvent.AfterImageEffectsOpaque, m_CommandBuffer);
        m_ForceMaterial = new Material(Shader.Find("Hidden/Force"));

        m_CurTexture = RenderTexture.GetTemporary(texSize, texSize, 16);
        m_CurTexture.name = "[Cur]";
        m_PreTexture = RenderTexture.GetTemporary(texSize, texSize, 16);
        m_PreTexture.name = "[Pre]";
        m_HeightMap = RenderTexture.GetTemporary(texSize, texSize, 16);
        m_HeightMap.name = "[HeightMap]";
        m_NormalMap = RenderTexture.GetTemporary(texSize, texSize, 16);
        m_NormalMap.anisoLevel = 1;
        m_NormalMap.name = "[NormalMap]";

        RenderTexture tmp = RenderTexture.active;
        RenderTexture.active = m_CurTexture;
        GL.Clear(false, true, new Color(0, 0, 0, 0));
        RenderTexture.active = m_PreTexture;
        GL.Clear(false, true, new Color(0, 0, 0, 0));
        RenderTexture.active = m_HeightMap;
        GL.Clear(false, true, new Color(0, 0, 0, 0));

        RenderTexture.active = tmp;
        
        m_Camera.targetTexture = m_CurTexture;

        Shader.SetGlobalFloat("internal_Force", force);
        
        m_WaveEquationMat = new Material(Shader.Find("Hidden/WaveEquationGen"));        
        m_WaveEquationMat.SetTexture("_Mask", mask);        
        m_NormalGenerateMat = new Material(Shader.Find("Hidden/NormalGen"));        
        m_WaveEquationMat.SetVector("_WaveParams", m_WaveParams);
    }

    /// <summary>
    /// 用贴图保存顶点偏移值
    /// </summary>
    /// <param name="src">当前cube的顶点到水面距离值</param>
    /// <param name="dst"></param>
    void OnRenderImage(RenderTexture src, RenderTexture dst)
    {
        // 在二维波动方程中当作z(i,j,k-1)上一个时间段的深度值
        m_WaveEquationMat.SetTexture("_PreTex", m_PreTexture);
        // src的MainTex为 z(i,j,k)当前时间段的深度值
        // 计算dst z(i,j,k+1)下一个时间段的深度值
        Graphics.Blit(src, dst, m_WaveEquationMat);
        
        Graphics.Blit(dst, m_HeightMap);

        Graphics.Blit(dst, m_NormalMap, m_NormalGenerateMat);

        Graphics.Blit(src, m_PreTexture);

    }

    void OnPostRender()
    {
        m_CommandBuffer.Clear();
        m_CommandBuffer.ClearRenderTarget(true, false, Color.black);
        m_CommandBuffer.SetRenderTarget(m_CurTexture);

        Shader.SetGlobalTexture("_LiquidHeightMap", m_HeightMap);
        Shader.SetGlobalTexture("_LiquidNormalMap", m_NormalMap);
    }


    void OnDestroy()
    {
        if (m_CurTexture)
            RenderTexture.ReleaseTemporary(m_CurTexture);
        if (m_PreTexture)
            RenderTexture.ReleaseTemporary(m_PreTexture);
        if (m_HeightMap)
            RenderTexture.ReleaseTemporary(m_HeightMap);
        if (m_NormalMap)
            RenderTexture.ReleaseTemporary(m_NormalMap);
        if (m_ForceMaterial)
            Destroy(m_ForceMaterial);
        if (m_WaveEquationMat)
            Destroy(m_WaveEquationMat);
        if (m_NormalGenerateMat)
            Destroy(m_NormalGenerateMat);
        if (m_CommandBuffer != null)
            m_CommandBuffer.Release();
    }
}
