using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

public class PCV_OperationHandler : MonoBehaviour
{
    [SerializeField] private PCV_Settings settings;
    [SerializeField] private PCV_DataManager dataManager;

    public void ExecuteVoxelDensityFilter()
    {
        if (dataManager.CurrentData == null || dataManager.SpatialSearch == null)
        {
            UnityEngine.Debug.LogWarning("�_�Q�f�[�^�����[�h����Ă��܂���B�����͎��s�s�\�ł��B");
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        int originalCount = dataManager.CurrentData.PointCount;
        UnityEngine.Debug.Log($"�{�N�Z�����x�t�B���^�����O���J�n���܂��B(臒l: {settings.voxelDensityThreshold})");

        var filteredData = FilterByVoxelDensity(dataManager.CurrentData, dataManager.SpatialSearch.VoxelGrid, settings.voxelDensityThreshold);

        stopwatch.Stop();
        LogFilteringResult("�{�N�Z�����x�t�B���^�����O", originalCount, filteredData.PointCount, stopwatch.ElapsedMilliseconds);
        dataManager.SetData(filteredData, settings.voxelSize);
    }

    public void ExecuteNoiseFilter()
    {
        if (dataManager.CurrentData == null || dataManager.SpatialSearch == null)
        {
            UnityEngine.Debug.LogWarning("�_�Q�f�[�^�����[�h����Ă��܂���B�����͎��s�s�\�ł��B");
            return;
        }

        if (settings.pointCloudFilterShader != null)
        {
            ExecuteNoiseFilteringGPU();
        }
        else
        {
            UnityEngine.Debug.LogWarning("�ߖT�T���m�C�Y�t�B���^�[Compute Shader���ݒ肳��Ă��܂���BCPU�ŏ��������s���܂��B");
            if (UnityEngine.Application.isPlaying)
            {
                StartCoroutine(ExecuteNoiseFilteringCPUCoroutine());
            }
            else
            {
                ExecuteNoiseFilteringCPU();
            }
        }
    }

    public void ExecuteMorphologyOperation()
    {
        if (dataManager.CurrentData == null)
        {
            UnityEngine.Debug.LogWarning("�_�Q�f�[�^�����[�h����Ă��܂���B�����͎��s�s�\�ł��B");
            return;
        }
        if (settings.morpologyOperationShader == null)
        {
            UnityEngine.Debug.LogWarning("�����t�H���W�[���ZCompute Shader���ݒ肳��Ă��܂���B");
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        int originalCount = dataManager.CurrentData.PointCount;
        UnityEngine.Debug.Log($"GPU�ɂ�郂���t�H���W�[���Z���J�n���܂��B(�N�H: {settings.erosionIterations}��, �c��: {settings.dilationIterations}��)");

        PCV_Data filteredData = PCV_MorphologyFilter.ApplyGPU(dataManager.CurrentData, settings.morpologyOperationShader, settings.voxelSize, settings.erosionIterations, settings.dilationIterations);

        stopwatch.Stop();
        LogFilteringResult("�����t�H���W�[���Z", originalCount, filteredData.PointCount, stopwatch.ElapsedMilliseconds);
        dataManager.SetData(filteredData, settings.voxelSize);
    }

    private PCV_Data FilterByVoxelDensity(PCV_Data inputData, VoxelGrid voxelGrid, int densityThreshold)
    {
        var passedPointIndices = new HashSet<int>();

        foreach (var voxelContent in voxelGrid.Grid)
        {
            if (voxelContent.Value.Count >= densityThreshold)
            {
                foreach (int pointIndex in voxelContent.Value)
                {
                    passedPointIndices.Add(pointIndex);
                }
            }
        }

        var sortedIndices = passedPointIndices.ToList();
        sortedIndices.Sort();

        var filteredVertices = new Vector3[sortedIndices.Count];
        var filteredColors = new Color[sortedIndices.Count];

        for (int i = 0; i < sortedIndices.Count; i++)
        {
            int originalIndex = sortedIndices[i];
            filteredVertices[i] = inputData.Vertices[originalIndex];
            filteredColors[i] = inputData.Colors[originalIndex];
        }

        return new PCV_Data(filteredVertices, filteredColors);
    }

    private void ExecuteNoiseFilteringCPU()
    {
        var stopwatch = Stopwatch.StartNew();
        int originalCount = dataManager.CurrentData.PointCount;
        UnityEngine.Debug.Log($"CPU�ɂ��m�C�Y�����������J�n���܂��B(臒l: {settings.neighborThreshold})");

        PCV_Data filteredData = PCV_NoiseFilter.FilterCPU(dataManager.CurrentData, dataManager.SpatialSearch.VoxelGrid, settings.searchRadius, settings.neighborThreshold);

        stopwatch.Stop();
        LogFilteringResult("�ߖT�T���m�C�Y����", originalCount, filteredData.PointCount, stopwatch.ElapsedMilliseconds);
        dataManager.SetData(filteredData, settings.voxelSize);
    }

    private IEnumerator ExecuteNoiseFilteringCPUCoroutine()
    {
        var stopwatch = Stopwatch.StartNew();
        int originalCount = dataManager.CurrentData.PointCount;
        UnityEngine.Debug.Log($"CPU�ɂ��m�C�Y��������(�R���[�`��)���J�n���܂��B(臒l: {settings.neighborThreshold})");

        PCV_Data result = null;
        yield return PCV_NoiseFilter.FilterCPUCoroutine(dataManager.CurrentData, dataManager.SpatialSearch.VoxelGrid, settings.searchRadius, settings.neighborThreshold,
            (filteredData) => { result = filteredData; }
        );

        stopwatch.Stop();
        LogFilteringResult("�ߖT�T���m�C�Y����", result.PointCount, result.PointCount, stopwatch.ElapsedMilliseconds);
        dataManager.SetData(result, settings.voxelSize);
    }

    private void ExecuteNoiseFilteringGPU()
    {
        var stopwatch = Stopwatch.StartNew();
        int originalCount = dataManager.CurrentData.PointCount;
        UnityEngine.Debug.Log($"GPU�ɂ��ߖT�T���m�C�Y�����������J�n���܂��B(臒l: {settings.neighborThreshold})");

        PCV_Data filteredData = PCV_NoiseFilter.FilterGPU(dataManager.CurrentData, settings.pointCloudFilterShader, settings.searchRadius, settings.neighborThreshold);

        stopwatch.Stop();
        LogFilteringResult("�ߖT�T���m�C�Y����", originalCount, filteredData.PointCount, stopwatch.ElapsedMilliseconds);
        dataManager.SetData(filteredData, settings.voxelSize);
    }

    private void LogFilteringResult(string filterName, int originalCount, int filteredCount, long elapsedMilliseconds)
    {
        UnityEngine.Debug.Log($"{filterName}�������������܂����B��������: {elapsedMilliseconds} ms. ���̓_��: {originalCount}, ������̓_��: {filteredCount}");
        if (filteredCount == 0)
        {
            UnityEngine.Debug.LogWarning("�S�Ă̓_����������܂����B���b�V���͋�ɂȂ�܂��B");
        }
    }
}