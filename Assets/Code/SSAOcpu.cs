using System.Collections.Generic;
using UnityEngine;

public class SSAOcpu : MonoBehaviour
{
    public ComputeShader ssaoShader;
    public ComputeShader blurShader;
    
    private Texture2D _ssaoNoise;

    private Camera _camera;
    private RenderTexture _depthResultRenderTexture;
    private RenderTexture _ssaoResultRenderTexture;
    private RenderTexture _blurResultRenderTexture;
    private RenderTexture _lightResultRenderTexture;

    [Header("SSAO Settings")]
    [SerializeField] float _total_strength = 1.0f;
    [SerializeField] float _base = 0.2f;
    [SerializeField] float _area = 0.0075f;
    [SerializeField] float _falloff = 0.000001f;
    [SerializeField] float _radius = 0.0002f;
    [SerializeField] float _bias = 0.025f;
    [SerializeField] float _threshold = 1.0f;
    [SerializeField] bool _debugMode = false;
    
    [Header("Blur Settings")]
    [SerializeField] int _blurKernelSize = 2;
    [SerializeField] float _blurTexelSizeMultiplier = 1.0f;
    [SerializeField] float _blurResultDivider = 16.0f;

    private Matrix4x4 _projection;

    private List<float> _samples;

    private int _ssaoKernel;
    private int _blurKernel;

    private void InitCamera()
    {
        _camera = GetComponent<Camera>();
        _camera.depthTextureMode = _camera.depthTextureMode | DepthTextureMode.DepthNormals;
    }

    private void InitRenderTextures()
    {
        if (_ssaoResultRenderTexture == null)
        {
            _ssaoResultRenderTexture = new RenderTexture(_camera.pixelWidth, _camera.pixelHeight, 24); //orig depth: 24
            _ssaoResultRenderTexture.enableRandomWrite = true;
            _ssaoResultRenderTexture.Create();
        }
        
        if (_blurResultRenderTexture == null)
        {
            _blurResultRenderTexture = new RenderTexture(_camera.pixelWidth, _camera.pixelHeight, 24);
            _blurResultRenderTexture.enableRandomWrite = true;
            _blurResultRenderTexture.Create();
        }
    }

    private void InitNoiseTexture()
    {
        _ssaoNoise = new Texture2D(4, 4);
        for (int i = 0; i < 4; i++)
        {
            for (int j = 0; j < 4; j++)
            {
                Color randomColor = new Color(
                    UnityEngine.Random.Range(0f, 1f),
                    UnityEngine.Random.Range(0f, 1f),
                    0f,
                    1f
                );
                _ssaoNoise.SetPixel(i, j, randomColor);
            }
        }

        _ssaoNoise.wrapMode = TextureWrapMode.Repeat;
        _ssaoNoise.Apply(false);
    }

    private void InitSamples()
    {
        _samples = new List<float>();
        for (int i = 0; i < 128; i++)
        {
            var x = UnityEngine.Random.Range(0f, 1f) * 2.0f - 1.0f;
            var y = UnityEngine.Random.Range(0f, 1f) * 2.0f - 1.0f;
            var z = UnityEngine.Random.Range(0f, 1f);
            var v = new Vector3(x, y, z).normalized;
            v *= UnityEngine.Random.Range(-1f, 1f);
            float scale = (float) i / 128.0f;
            scale = lerp(0.1f, 1.0f, scale * scale);
            v *= scale;
            _samples.Add(v.x);
            _samples.Add(v.y);
            _samples.Add(v.z);
        }
    }

    private void InitKernels()
    {
        _ssaoKernel = ssaoShader.FindKernel("CSMain");
        _blurKernel = blurShader.FindKernel("CSMain");
    }
    
    private void Awake()
    {
        InitCamera();
        InitRenderTextures();
        InitNoiseTexture();
        InitSamples();
        InitKernels();
    }

    float lerp(float a, float b, float f)
    {
        return a + f * (b - a);
    }

    private void UpdateSSAOShaderValues(RenderTexture src)
    {
        ssaoShader.SetTexture(_ssaoKernel, "Src", src);
        ssaoShader.SetTexture(_ssaoKernel, "Result", _ssaoResultRenderTexture);

        ssaoShader.SetFloats("samples", _samples.ToArray());
        
        ssaoShader.SetTextureFromGlobal(_ssaoKernel, "_CameraDepthNormalsTexture", "_CameraDepthNormalsTexture");
        ssaoShader.SetTextureFromGlobal(_ssaoKernel, "_CameraDepthTexture", "_CameraDepthTexture");
        ssaoShader.SetTexture(_ssaoKernel, "RandomTextureSampler", _ssaoNoise);

        ssaoShader.SetFloat("total_strength", _total_strength);
        ssaoShader.SetFloat("base", _base);
        ssaoShader.SetFloat("area", _area);
        ssaoShader.SetFloat("falloff", _falloff);
        ssaoShader.SetFloat("radius", _radius * _camera.pixelWidth/10);
        ssaoShader.SetFloat("bias", _bias);
        ssaoShader.SetFloat("threshold", _threshold);
        
        ssaoShader.SetFloat("_width", _camera.pixelWidth);
        ssaoShader.SetFloat("_height", _camera.pixelHeight);

        var matrix = Matrix4x4.Perspective(_camera.fieldOfView, _camera.aspect, _camera.nearClipPlane, _camera.farClipPlane);
        var shaderMatrix = GL.GetGPUProjectionMatrix( matrix, false);
        ssaoShader.SetMatrix("projection", shaderMatrix);
        ssaoShader.SetMatrix("g_matInvProjection", shaderMatrix.inverse);
        var viewMatrixInv = _camera.worldToCameraMatrix.inverse;
        var shaderViewMatrixInv = GL.GetGPUProjectionMatrix( viewMatrixInv, false);
        ssaoShader.SetMatrix("viewMatrixInv", shaderViewMatrixInv);
        
        ssaoShader.SetBool("debugMode", _debugMode);
    }

    private void UpdateBlurShaderValues(RenderTexture src)
    {
        blurShader.SetTexture(_blurKernel, "Src", src);
        blurShader.SetTexture(_blurKernel, "ssaoInput", _ssaoResultRenderTexture);
        blurShader.SetTexture(_blurKernel, "Result", _blurResultRenderTexture);
        blurShader.SetFloat("_width", _camera.pixelWidth);
        blurShader.SetFloat("_height", _camera.pixelHeight);
        blurShader.SetBool("debug", _debugMode);
        blurShader.SetInt("_blurKernelSize", _blurKernelSize);
        blurShader.SetFloat("_blurTexelSizeMultiplier", _blurTexelSizeMultiplier);
        blurShader.SetFloat("_blurResultDivider", _blurResultDivider);
    }
    
    private void OnRenderImage(RenderTexture src, RenderTexture dest)
    {
        UpdateSSAOShaderValues(src);
        ssaoShader.Dispatch(_ssaoKernel, _ssaoResultRenderTexture.width/8, _ssaoResultRenderTexture.height/8, 1);
        
        UpdateBlurShaderValues(src);
        blurShader.Dispatch(_blurKernel, _blurResultRenderTexture.width/8, _blurResultRenderTexture.height/8, 1);
        
        Graphics.Blit(_blurResultRenderTexture, dest);
    }
}
