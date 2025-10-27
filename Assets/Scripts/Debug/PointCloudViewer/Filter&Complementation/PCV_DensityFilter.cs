using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;

public static class PCV_DensityFilter
{
    public static void Execute(PCV_DataManager dataManager, PCV_Settings settings)
    {
        if (dataManager.CurrentData == null || dataManager.SpatialSearch == null)
        {
            UnityEngine.Debug.LogWarning("�_�Q�f�[�^�����[�h����Ă��܂���B�����͎��s�s�\�ł��B");
            return;
        }

        PCV_Data filteredData;
        var stopwatch = Stopwatch.StartNew();
        int originalCount = dataManager.CurrentData.PointCount;

        if (settings.useGpuDensityFilter && settings.densityFilterShader != null)
        {
            UnityEngine.Debug.Log($"GPU�ɂ��{�N�Z�����x�t�B���^�����O���J�n���܂��B(臒l: {settings.voxelDensityThreshold})");
            filteredData = ApplyGPU(
                dataManager.CurrentData,
                dataManager.SpatialSearch.VoxelGrid,
                settings.densityFilterShader,
                settings.voxelDensityThreshold
            );
            stopwatch.Stop();
            LogFilteringResult("�{�N�Z�����x�t�B���^�����O (GPU)", originalCount, filteredData.PointCount, stopwatch.ElapsedMilliseconds);
        }
        else
        {
            if (!settings.useGpuDensityFilter)
            {
                UnityEngine.Debug.Log("CPU���s���I������Ă��܂��BCPU�Ń{�N�Z�����x�t�B���^�����O�����s���܂��B");
            }
            else if (settings.densityFilterShader == null)
            {
                UnityEngine.Debug.LogWarning("GPU���s���I������Ă��܂����A���x�t�B���^�����OCompute Shader���ݒ肳��Ă��܂���BCPU�ŏ��������s���܂��B");
            }
            
            filteredData = ApplyCPU(dataManager.CurrentData, dataManager.SpatialSearch.VoxelGrid, settings.voxelDensityThreshold);
            stopwatch.Stop();
            LogFilteringResult("�{�N�Z�����x�t�B���^�����O (CPU)", originalCount, filteredData.PointCount, stopwatch.ElapsedMilliseconds);
        }

        dataManager.SetData(filteredData, settings.voxelSize);
    }

    public static PCV_Data ApplyGPU(PCV_Data data, VoxelGrid voxelGrid, ComputeShader computeShader, int densityThreshold)
    {
        if (data == null || data.PointCount == 0 || computeShader == null || voxelGrid == null)
        {
            UnityEngine.Debug.LogError("[PCV_DensityFilter] Invalid input parameters");
            return new PCV_Data(new List<Vector3>(), new List<Color>());
        }

        if (voxelGrid.VoxelDataBuffer == null || voxelGrid.OriginalPointsBuffer == null || voxelGrid.VoxelPointIndicesBuffer == null)
        {
            UnityEngine.Debug.LogError("[PCV_DensityFilter] VoxelGrid��GPU�o�b�t�@������������Ă��܂���B");
            return data;
        }

        int voxelCount = voxelGrid.VoxelDataBuffer.count;

        if (voxelCount == 0)
        {
            UnityEngine.Debug.LogWarning("[PCV_DensityFilter] No voxels to process");
            return new PCV_Data(new List<Vector3>(), new List<Color>());
        }

        ComputeBuffer filteredPointsBuffer = null;
        ComputeBuffer countBuffer = null;

        try
        {
            filteredPointsBuffer = new ComputeBuffer(data.PointCount, sizeof(float) * 8, ComputeBufferType.Append);
            filteredPointsBuffer.SetCounterValue(0);

            int kernel = computeShader.FindKernel("CSDensityFilter");
            computeShader.SetInt("_DensityThreshold", densityThreshold);
            computeShader.SetBuffer(kernel, "_VoxelData", voxelGrid.VoxelDataBuffer);
            computeShader.SetBuffer(kernel, "_VoxelPointIndices", voxelGrid.VoxelPointIndicesBuffer);
            computeShader.SetBuffer(kernel, "_PointsIn", voxelGrid.OriginalPointsBuffer);
            computeShader.SetBuffer(kernel, "_PointsOut", filteredPointsBuffer);

            int threadGroups = Mathf.CeilToInt(voxelCount / 64.0f);
            computeShader.Dispatch(kernel, threadGroups, 1, 1);

            countBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Raw);
            ComputeBuffer.CopyCount(filteredPointsBuffer, countBuffer, 0);

            int[] countArray = { 0 };
            countBuffer.GetData(countArray);
            int filteredPointCount = countArray[0];

            var filteredVertices = new List<Vector3>(filteredPointCount);
            var filteredColors = new List<Color>(filteredPointCount);

            if (filteredPointCount > 0)
            {
                var filteredPointData = new PCV_Point_GPU[filteredPointCount]; 
                filteredPointsBuffer.GetData(filteredPointData, 0, 0, filteredPointCount);

                for (int i = 0; i < filteredPointCount; i++)
                {
                    filteredVertices.Add((Vector3)filteredPointData[i].position); 
                    filteredColors.Add(filteredPointData[i].color);
                }
            }

            return new PCV_Data(filteredVertices, filteredColors);
        }
        finally
        {
            if (filteredPointsBuffer != null && filteredPointsBuffer.IsValid())
            {
                filteredPointsBuffer.Release();
            }

            if (countBuffer != null && countBuffer.IsValid())
            {
                countBuffer.Release();
            }
        }
    }

    private static PCV_Data ApplyCPU(PCV_Data inputData, VoxelGrid voxelGrid, int densityThreshold)
    {
        var filteredVertices = new List<Vector3>(inputData.PointCount);
        var filteredColors = new List<Color>(inputData.PointCount);

        foreach (var voxelContent in voxelGrid.Grid)
        {
            if (voxelContent.Value.Count >= densityThreshold)
            {
                foreach (int pointIndex in voxelContent.Value)
                {
                    if (pointIndex >= 0 && pointIndex < inputData.PointCount)
                    {
                        filteredVertices.Add(inputData.Vertices[pointIndex]);
                        filteredColors.Add(inputData.Colors[pointIndex]);
                    }
                }
            }
        }

        return new PCV_Data(filteredVertices, filteredColors);
    }

    private static void LogFilteringResult(string filterName, int originalCount, int filteredCount, long elapsedMilliseconds)
    {
        UnityEngine.Debug.Log($"{filterName}�������������܂����B��������: {elapsedMilliseconds} ms. ���̓_��: {originalCount}, ������̓_��: {filteredCount}");
        if (filteredCount == 0)
        {
            UnityEngine.Debug.LogWarning("�S�Ă̓_����������܂����B���b�V���͋�ɂȂ�܂��B");
        }
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct PCV_Point_GPU
    {
        public Vector4 position;
        public Color color;
    }
}