using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;

public class Mipmapper {
  public ComputeShader compute {
    get {
      if (_compute == null) _compute = (ComputeShader)Resources.Load("VXGI/Compute/Mipmapper");

      return _compute;
    }
  }

  int _kernelFilter;
  int _kernelShift;
  int _propDisplacement;
  int _propDst;
  int _propDstRes;
  int _propSrc;
  CommandBuffer _commandFilter;
  CommandBuffer _commandShift;
  ComputeShader _compute;
  VXGI _vxgi;

  public Mipmapper(VXGI vxgi) {
    _vxgi = vxgi;

    _commandFilter = new CommandBuffer { name = "Mipmapper.Filter" };
    _commandShift = new CommandBuffer { name = "Mipmapper.Shift" };

    if (Application.platform == RuntimePlatform.LinuxEditor || Application.platform == RuntimePlatform.LinuxPlayer) {
      _kernelFilter = 1;
    } else {
      _kernelFilter = 0;
    }

    _kernelShift = compute.FindKernel("CSShift");

    _propDisplacement = Shader.PropertyToID("Displacement");
    _propDst = Shader.PropertyToID("Dst");
    _propDstRes = Shader.PropertyToID("DstRes");
    _propSrc = Shader.PropertyToID("Src");
  }

  public void Dispose() {
    _commandFilter.Dispose();
  }

  public void Filter(ScriptableRenderContext renderContext) {
    _commandFilter.BeginSample(_commandFilter.name);

    var radiances = _vxgi.radiances;

    for (var i = 1; i < radiances.Length; i++) {
      int resolution = radiances[i].volumeDepth;
      int groups = Mathf.CeilToInt((float)resolution / 8f);

      _commandFilter.SetComputeIntParam(compute, _propDstRes, resolution);
      _commandFilter.SetComputeTextureParam(compute, _kernelFilter, _propDst, radiances[i]);
      _commandFilter.SetComputeTextureParam(compute, _kernelFilter, _propSrc, radiances[i - 1]);
      _commandFilter.DispatchCompute(compute, _kernelFilter, groups, groups, groups);
    }

    _commandFilter.EndSample(_commandFilter.name);

    renderContext.ExecuteCommandBuffer(_commandFilter);

    _commandFilter.Clear();
  }

  public void Shift(ScriptableRenderContext renderContext, Vector3Int displacement) {
    _commandShift.BeginSample(_commandShift.name);

    int groups = Mathf.CeilToInt((float)_vxgi.resolution / 8f);

    _commandShift.SetComputeIntParam(compute, _propDstRes, (int)_vxgi.resolution);
    _commandShift.SetComputeIntParams(compute, _propDisplacement, new[] { displacement.x, displacement.y, displacement.z });
    _commandShift.SetComputeTextureParam(compute, _kernelShift, _propDst, _vxgi.radiances[0]);
    _commandShift.DispatchCompute(compute, _kernelShift, groups, groups, groups);

    _commandShift.EndSample(_commandShift.name);

    renderContext.ExecuteCommandBuffer(_commandShift);

    _commandShift.Clear();

    Filter(renderContext);
  }
}
