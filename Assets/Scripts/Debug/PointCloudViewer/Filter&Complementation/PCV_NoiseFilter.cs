using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using System;

public static class PCV_NoiseFilter
{
    public static void Execute(PCV_DataManager dataManager, PCV_Settings settings, MonoBehaviour coroutineRunner)
    {
        if (dataManager.CurrentData == null || dataManager.SpatialSearch == null)
        {
            UnityEngine.Debug.LogWarning("�_�Q�f�[�^�����[�h����Ă��܂���B�����͎��s�s�\�ł��B");
            return;
        }

        if (settings.useGpuNoiseFilter && settings.pointCloudFilterShader != null)
        {
            ExecuteGPU(dataManager, settings);
        }
        else
        {
            if (!settings.useGpuNoiseFilter)
            {
                UnityEngine.Debug.Log("CPU���s���I������Ă��܂��BCPU�Ńm�C�Y���������s���܂��B");
            }
            else if (settings.pointCloudFilterShader == null)
            {
                UnityEngine.Debug.LogWarning("GPU���s���I������Ă��܂����A�ߖT�T���m�C�Y�t�B���^�[Compute Shader���ݒ肳��Ă��܂���BCPU�ŏ��������s���܂��B");
            }
            if (UnityEngine.Application.isPlaying)
            {
                coroutineRunner.StartCoroutine(ExecuteCPUCoroutine(dataManager, settings));
            }
            else
            {
                ExecuteCPU(dataManager, settings);
            }
        }
    }

    private static void ExecuteCPU(PCV_DataManager dataManager, PCV_Settings settings)
    {
        var stopwatch = Stopwatch.StartNew();
        int originalCount = dataManager.CurrentData.PointCount;
        UnityEngine.Debug.Log($"CPU�ɂ��m�C�Y�����������J�n���܂��B(臒l: {settings.neighborThreshold})");

        PCV_Data filteredData = FilterCPU(dataManager.CurrentData, dataManager.SpatialSearch.VoxelGrid, settings.searchRadius, settings.neighborThreshold);

        stopwatch.Stop();
        LogFilteringResult("�ߖT�T���m�C�Y����", originalCount, filteredData.PointCount, stopwatch.ElapsedMilliseconds);
        dataManager.SetData(filteredData, settings.voxelSize);
    }

    private static IEnumerator ExecuteCPUCoroutine(PCV_DataManager dataManager, PCV_Settings settings)
    {
        var stopwatch = Stopwatch.StartNew();
        int originalCount = dataManager.CurrentData.PointCount;
        UnityEngine.Debug.Log($"CPU�ɂ��m�C�Y��������(�R���[�`��)���J�n���܂��B(臒l: {settings.neighborThreshold})");

        PCV_Data result = null;
        yield return FilterCPUCoroutine(dataManager.CurrentData, dataManager.SpatialSearch.VoxelGrid, settings.searchRadius, settings.neighborThreshold,
            (filteredData) => { result = filteredData; }
        );

        stopwatch.Stop();
        LogFilteringResult("�ߖT�T���m�C�Y����", originalCount, result.PointCount, stopwatch.ElapsedMilliseconds);
        dataManager.SetData(result, settings.voxelSize);
    }

    private static void ExecuteGPU(PCV_DataManager dataManager, PCV_Settings settings)
    {
        var stopwatch = Stopwatch.StartNew();
        int originalCount = dataManager.CurrentData.PointCount;
        UnityEngine.Debug.Log($"GPU�ɂ��ߖT�T���m�C�Y�����������J�n���܂��B(臒l: {settings.neighborThreshold})");

        PCV_Data filteredData = FilterGPU(dataManager.CurrentData, settings.pointCloudFilterShader, settings.searchRadius, settings.neighborThreshold);

        stopwatch.Stop();
        LogFilteringResult("�ߖT�T���m�C�Y����", originalCount, filteredData.PointCount, stopwatch.ElapsedMilliseconds);
        dataManager.SetData(filteredData, settings.voxelSize);
    }

    private static void LogFilteringResult(string filterName, int originalCount, int filteredCount, long elapsedMilliseconds)
    {
        UnityEngine.Debug.Log($"{filterName}�������������܂����B��������: {elapsedMilliseconds} ms. ���̓_��: {originalCount}, ������̓_��: {filteredCount}");
        if (filteredCount == 0)
        {
            UnityEngine.Debug.LogWarning("�S�Ă̓_����������܂����B���b�V���͋�ɂȂ�܂��B");
        }
    }

    public static PCV_Data FilterCPU(PCV_Data data, VoxelGrid voxelGrid, float searchRadius, int threshold)
    {
        if (data == null || data.PointCount == 0 || voxelGrid == null)
        {
            return new PCV_Data(new List<Vector3>(), new List<Color>());
        }

        var filteredVertices = new List<Vector3>();
        var filteredColors = new List<Color>();

        for (int i = 0; i < data.PointCount; i++)
        {
            List<int> neighbors = voxelGrid.FindNeighbors(i, searchRadius);
            if (neighbors.Count >= threshold)
            {
                filteredVertices.Add(data.Vertices[i]);
                filteredColors.Add(data.Colors[i]);
            }
        }
        return new PCV_Data(filteredVertices, filteredColors);
    }

    public static IEnumerator FilterCPUCoroutine(PCV_Data data, VoxelGrid voxelGrid, float searchRadius, int threshold, Action<PCV_Data> onComplete)
    {
        if (data == null || data.PointCount == 0 || voxelGrid == null)
        {
            onComplete?.Invoke(new PCV_Data(new List<Vector3>(), new List<Color>()));
            yield break;
        }

        var filteredVertices = new List<Vector3>();
        var filteredColors = new List<Color>();
        int pointsPerFrame = 5000;

        for (int i = 0; i < data.PointCount; i++)
        {
            List<int> neighbors = voxelGrid.FindNeighbors(i, searchRadius);
            if (neighbors.Count >= threshold)
            {
                filteredVertices.Add(data.Vertices[i]);
                filteredColors.Add(data.Colors[i]);
            }

            if (i > 0 && (i + 1) % pointsPerFrame == 0)
            {
                yield return null;
            }
        }
        onComplete?.Invoke(new PCV_Data(filteredVertices, filteredColors));
    }

    public static PCV_Data FilterGPU(PCV_Data data, ComputeShader computeShader, float searchRadius, int threshold)
    {
        if (data == null || data.PointCount == 0)
        {
            return new PCV_Data(new List<Vector3>(), new List<Color>());
        }

        var pointData = new PCV_Point[data.PointCount];
        for (int i = 0; i < data.PointCount; i++)
        {
            pointData[i] = new PCV_Point { position = data.Vertices[i], color = data.Colors[i] };
        }

        int pointStructSize = sizeof(float) * 8;

        var pointsBuffer = new ComputeBuffer(data.PointCount, pointStructSize);
        var filteredPointsBuffer = new ComputeBuffer(data.PointCount, pointStructSize, ComputeBufferType.Append);
        var countBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Raw);

        try
        {
            pointsBuffer.SetData(pointData);
            filteredPointsBuffer.SetCounterValue(0);

            int kernel = computeShader.FindKernel("CSMain");
            computeShader.SetInt("_PointCount", data.PointCount);
            computeShader.SetFloat("_SearchRadius", searchRadius);
            computeShader.SetInt("_NeighborThreshold", threshold);
            computeShader.SetBuffer(kernel, "_Points", pointsBuffer);
            computeShader.SetBuffer(kernel, "_FilteredPoints", filteredPointsBuffer);

            int threadGroups = Mathf.CeilToInt(data.PointCount / 64.0f);
            computeShader.Dispatch(kernel, threadGroups, 1, 1);

            ComputeBuffer.CopyCount(filteredPointsBuffer, countBuffer, 0);
            int[] countArray = { 0 };
            countBuffer.GetData(countArray);
            int filteredPointCount = countArray[0];

            var filteredVertices = new List<Vector3>();
            var filteredColors = new List<Color>();

            if (filteredPointCount > 0)
            {
                var filteredPointData = new PCV_Point[filteredPointCount];
                filteredPointsBuffer.GetData(filteredPointData, 0, 0, filteredPointCount);
                for (int i = 0; i < filteredPointCount; i++)
                {
                    filteredVertices.Add(filteredPointData[i].position);
                    filteredColors.Add(filteredPointData[i].color);
                }
            }
            return new PCV_Data(filteredVertices, filteredColors);
        }
        finally
        {
            pointsBuffer.Release();
            filteredPointsBuffer.Release();
            countBuffer.Release();
        }
    }
}
