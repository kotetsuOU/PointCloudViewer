using UnityEngine;

[System.Serializable]
public struct FileSettings
{
    public bool useFile;
    public string filePath;
    public Color color;

    public bool IsDifferent(FileSettings other)
    {
        return useFile != other.useFile || filePath != other.filePath || color != other.color;
    }
}

public class PCV_Settings : MonoBehaviour
{
    public FileSettings[] fileSettings = new FileSettings[4]
    {
        new FileSettings { useFile = true,  filePath = "Assets/HandTrakingData/PointCloudData/currentGlobalVerticesRight.txt",  color = Color.red },
        new FileSettings { useFile = false, filePath = "Assets/HandTrakingData/PointCloudData/currentGlobalVerticesLeft.txt",   color = Color.green },
        new FileSettings { useFile = false, filePath = "Assets/HandTrakingData/PointCloudData/currentGlobalVerticesBottom.txt", color = Color.blue },
        new FileSettings { useFile = false, filePath = "Assets/HandTrakingData/PointCloudData/currentGlobalVerticesTop.txt",    color = Color.yellow }
    };

    public float pointSize = 0.01f;
    public GameObject outline;
    public Color outlineColor = Color.white;

    [Tooltip("��ԕ����O���b�h�̊e�Z���̃T�C�Y")]
    public float voxelSize = 0.05f;
    [Tooltip("�_�̎��͂ŋߖT�_��T�����锼�a")]
    public float searchRadius = 0.1f;
    [Tooltip("�ߖT�_���n�C���C�g����F")]
    public Color neighborColor = Color.cyan;
    [Tooltip("�m�C�Y�Ɣ��f����ߖT�_��臒l")]
    public int neighborThreshold = 100;
    [Tooltip("�m�C�Y�Ɣ��f����{�N�Z�����̍ŏ��_��")]
    public int voxelDensityThreshold = 5;

    [Tooltip("�N�H�����̔�����")]
    public int erosionIterations = 1;
    [Tooltip("�c�������̔�����")]
    public int dilationIterations = 1;

    [Tooltip("�⊮���s���{�N�Z�����̍ŏ��_��")]
    public int complementationDensityThreshold = 5;
    [Tooltip("�⊮���Ƀ{�N�Z�����Ƃɒǉ�����_��1�ӂ̐� (��: 2 = 4�_, 3 = 9�_)")]
    public uint complementationPointsPerAxis = 2;
    [Tooltip("�⊮���ɒǉ�����_�̐F")]
    public Color complementationPointColor = Color.purple;
    [Tooltip("�L���ȃ{�N�Z�����ɓ_�������_���ɔz�u���܂��B")]
    public bool complementationRandomPlacement = false;

    [Tooltip("�e�������R���[�`���Ŏ��s���A�t���[�����[�g�̒ቺ��h��")]
    public bool useCoroutine = false;

    [Header("GPU Acceleration")]
    [Tooltip("�_�Q�t�B���^�����O�Ɏg�p����Compute Shader")]
    public ComputeShader pointCloudFilterShader;
    [Tooltip("�`�Ԋw�I����Ɏg�p����Compute Shader")]
    public ComputeShader morpologyOperationShader;
    [Tooltip("�{�N�Z�����x�t�B���^�����O�Ɏg�p����Compute Shader")]
    public ComputeShader densityFilterShader;
    [Tooltip("���x�⊮�Ɏg�p����Compute Shader")]
    public ComputeShader densityComplementationShader;
    [Tooltip("�{�N�Z���O���b�h�\�z�Ɏg�p����Compute Shader")]
    public ComputeShader voxelGridBuilderShader;

    [Tooltip("�ߖT�T���m�C�Y������GPU���g�p����")]
    public bool useGpuNoiseFilter = true;
    [Tooltip("�{�N�Z�����x�t�B���^�����O��GPU���g�p����")]
    public bool useGpuDensityFilter = true;
    [Tooltip("���x�⊮��GPU���g�p����")]
    public bool useGpuDensityComplementation = true;

    private FileSettings[] lastFileSettings;
    private float lastPointSize;
    private GameObject lastOutline;
    private Color lastOutlineColor;
    private float lastVoxelSize;
    private float lastSearchRadius;
    private Color lastNeighborColor;
    private int lastNeighborThreshold;
    private int lastVoxelDensityThreshold;

    private int lastErosionIterations;
    private int lastDilationIterations;

    private int lastComplementationDensityThreshold;
    private uint lastComplementationPointsPerAxis;
    private Color lastComplementationPointColor;
    private bool lastComplementationRandomPlacement;

    private ComputeShader lastPointCloudFilterShader;
    private ComputeShader lastMorpologyOperationShader;
    private ComputeShader lastDensityFilterShader;
    private ComputeShader lastDensityComplementationShader;
    private ComputeShader lastVoxelGridBuilderShader;

    private bool lastUseGpuNoiseFilter;
    private bool lastUseGpuDensityFilter;
    private bool lastUseGpuDensityComplementation;


    private void Awake()
    {
        SaveInspectorState();
    }

    public void SaveInspectorState()
    {
        lastFileSettings = new FileSettings[fileSettings.Length];
        for (int i = 0; i < fileSettings.Length; i++)
        {
            lastFileSettings[i] = fileSettings[i];
        }
        lastPointSize = pointSize;
        lastOutline = outline;
        lastOutlineColor = outlineColor;
        lastVoxelSize = voxelSize;
        lastSearchRadius = searchRadius;
        lastNeighborColor = neighborColor;
        lastNeighborThreshold = neighborThreshold;
        lastVoxelDensityThreshold = voxelDensityThreshold;

        lastErosionIterations = erosionIterations;
        lastDilationIterations = dilationIterations;

        lastComplementationDensityThreshold = complementationDensityThreshold;
        lastComplementationPointsPerAxis = complementationPointsPerAxis;
        lastComplementationPointColor = complementationPointColor;
        lastComplementationRandomPlacement = complementationRandomPlacement;

        lastPointCloudFilterShader = pointCloudFilterShader;
        lastMorpologyOperationShader = morpologyOperationShader;
        lastDensityFilterShader = densityFilterShader;
        lastDensityComplementationShader = densityComplementationShader;
        lastVoxelGridBuilderShader = voxelGridBuilderShader;

        lastUseGpuNoiseFilter = useGpuNoiseFilter;
        lastUseGpuDensityFilter = useGpuDensityFilter;
        lastUseGpuDensityComplementation = useGpuDensityComplementation;
    }

    public bool HasFileSettingsChanged()
    {
        if (lastFileSettings == null || lastFileSettings.Length != fileSettings.Length) return true;

        for (int i = 0; i < fileSettings.Length; i++)
        {
            if (fileSettings[i].IsDifferent(lastFileSettings[i]))
            {
                return true;
            }
        }
        return false;
    }

    public bool HasRenderingSettingsChanged()
    {
        return pointSize != lastPointSize || outlineColor != lastOutlineColor || outline != lastOutline;
    }

    public bool HasMorpologySettingsChanged()
    {
        return erosionIterations != lastErosionIterations || dilationIterations != lastDilationIterations || morpologyOperationShader != lastMorpologyOperationShader;
    }

    public bool HasComplementationSettingsChanged()
    {
        return complementationDensityThreshold != lastComplementationDensityThreshold ||
               complementationPointsPerAxis != lastComplementationPointsPerAxis ||
               complementationPointColor != lastComplementationPointColor ||
               complementationRandomPlacement != lastComplementationRandomPlacement;
    }

    public bool HasProcessingSettingsChanged()
    {
        bool densityShadersChanged = (morpologyOperationShader != lastMorpologyOperationShader) ||
                                     (densityFilterShader != lastDensityFilterShader) ||
                                     (densityComplementationShader != lastDensityComplementationShader) ||
                                     (pointCloudFilterShader != lastPointCloudFilterShader) ||
                                     (voxelGridBuilderShader != lastVoxelGridBuilderShader);

        bool processingParamsChanged = voxelSize != lastVoxelSize ||
                                       searchRadius != lastSearchRadius ||
                                       neighborColor != lastNeighborColor ||
                                       neighborThreshold != lastNeighborThreshold ||
                                       voxelDensityThreshold != lastVoxelDensityThreshold;

        return processingParamsChanged ||
               densityShadersChanged ||
               HasMorpologySettingsChanged() ||
               HasComplementationSettingsChanged();
    }
}