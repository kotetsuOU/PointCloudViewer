using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using System.Linq;
using System.Runtime.InteropServices;

public static class PCV_DensityComplementation
{
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct Point
    {
        public Vector4 position;
        public Color color;
    }

    private const int POINT_SIZE = 32;

    public static void Execute(PCV_DataManager dataManager, PCV_Settings settings)
    {
        if (dataManager.CurrentData == null || dataManager.SpatialSearch == null || dataManager.SpatialSearch.VoxelGrid == null)
        {
            UnityEngine.Debug.LogWarning("�_�Q�f�[�^�܂���VoxelGrid������������Ă��܂���B�����͎��s�s�\�ł��B");
            return;
        }

        PCV_Data combinedData;
        var stopwatch = Stopwatch.StartNew();
        int originalCount = dataManager.CurrentData.PointCount;

        if (settings.useGpuDensityComplementation && settings.densityComplementationShader != null && settings.voxelGridBuilderShader != null)
        {
            UnityEngine.Debug.Log($"GPU�ɂ�閧�x�⊮�������J�n���܂��B");

            combinedData = ApplyGPU(
                dataManager.CurrentData,
                settings.densityComplementationShader,
                settings.voxelGridBuilderShader,
                settings
            );

            if (combinedData == null)
            {
                UnityEngine.Debug.LogError("GPU�⊮�������ɃG���[���������܂����B");
                stopwatch.Stop();
                return;
            }

            if (combinedData.PointCount == originalCount)
            {
                UnityEngine.Debug.LogWarning("臒l�𒴂���L���ȃ{�N�Z����������܂���ł����B�_�͒ǉ�����܂���B");
                stopwatch.Stop();
                return;
            }

            stopwatch.Stop();
            int addedCount = combinedData.PointCount - originalCount;
            UnityEngine.Debug.Log($"���x�⊮���� (GPU) ���������܂����B{addedCount} �_���ǉ�����܂����B��������: {stopwatch.ElapsedMilliseconds} ms.");
        }
        else
        {
            if (!settings.useGpuDensityComplementation)
            {
                UnityEngine.Debug.Log("CPU���s���I������Ă��܂��BCPU�Ŗ��x�⊮�����s���܂��B");
            }
            else if (settings.densityComplementationShader == null)
            {
                UnityEngine.Debug.LogWarning("GPU���s���I������Ă��܂����A���x�⊮Compute Shader���ݒ肳��Ă��܂���BCPU�ŏ��������s���܂��B");
            }
            else if (settings.voxelGridBuilderShader == null)
            {
                UnityEngine.Debug.LogWarning("GPU���s���I������Ă��܂����AVoxelGridBuilder Compute Shader���ݒ肳��Ă��܂���BCPU�ŏ��������s���܂��B");
            }

            combinedData = ApplyCPU(dataManager.CurrentData, dataManager.SpatialSearch.VoxelGrid, settings, stopwatch);
            if (combinedData == null)
            {
                stopwatch.Stop();
                return;
            }
        }

        dataManager.SetData(combinedData, settings.voxelSize);
    }

    public static PCV_Data ApplyGPU(PCV_Data data, ComputeShader computeShader, ComputeShader gridBuilderShader, PCV_Settings settings)
    {
        if (computeShader == null || gridBuilderShader == null || data == null)
        {
            return new PCV_Data(new List<Vector3>(), new List<Color>());
        }

        int originalPointCount = data.PointCount;
        if (originalPointCount == 0 || settings.complementationPointsPerAxis == 0)
        {
            return data;
        }

        uint pointsPerAxis = settings.complementationPointsPerAxis;
        uint totalPointsPerVoxel = (pointsPerAxis == 1) ? 1u : (pointsPerAxis * pointsPerAxis);

        ComputeBuffer pointsBuffer = null;
        ComputeBuffer newPointsBuffer = null;
        ComputeBuffer finalCombinedBuffer = null;
        ComputeBuffer countBuffer = null;
        PCV_GpuVoxelGrid gpuVoxelGrid = null;

        try
        {
            var pointArray = new Point[originalPointCount];
            for (int i = 0; i < originalPointCount; i++)
            {
                pointArray[i].position = new Vector4(data.Vertices[i].x, data.Vertices[i].y, data.Vertices[i].z, 0f);
                pointArray[i].color = data.Colors[i];
            }

            pointsBuffer = new ComputeBuffer(originalPointCount, POINT_SIZE);
            pointsBuffer.SetData(pointArray);

            gpuVoxelGrid = new PCV_GpuVoxelGrid(gridBuilderShader, settings.voxelSize);
            gpuVoxelGrid.AllocateBuffers(originalPointCount);
            gpuVoxelGrid.Build(pointsBuffer, originalPointCount);

            int voxelCount = gpuVoxelGrid.VoxelCount;
            if (voxelCount == 0)
            {
                return data;
            }

            int maxNewPoints = voxelCount * (int)totalPointsPerVoxel;
            int maxCombinedPoints = originalPointCount + maxNewPoints;

            newPointsBuffer = new ComputeBuffer(maxNewPoints, POINT_SIZE, ComputeBufferType.Append);
            newPointsBuffer.SetCounterValue(0);
            finalCombinedBuffer = new ComputeBuffer(maxCombinedPoints, POINT_SIZE);

            int kernelComp = computeShader.FindKernel("CSDensityComplementation");
            int kernelMerge = computeShader.FindKernel("CSMerge");
            int kernelBlit = computeShader.FindKernel("CSBlit");

            computeShader.SetInt("_DensityThreshold", settings.complementationDensityThreshold);
            computeShader.SetFloat("_VoxelSize", settings.voxelSize);
            computeShader.SetInt("_PointsPerAxis", (int)pointsPerAxis);
            computeShader.SetInt("_UseRandomPlacement", settings.complementationRandomPlacement ? 1 : 0);
            computeShader.SetVector("_ComplementationColor", settings.complementationPointColor);
            computeShader.SetFloat("_RandomSeed", Time.time);

            computeShader.SetBuffer(kernelComp, "_VoxelData", gpuVoxelGrid.VoxelDataBuffer);
            computeShader.SetBuffer(kernelComp, "_ComplementedPointsOut", newPointsBuffer);

            int threadGroups = Mathf.CeilToInt(voxelCount / 64.0f);
            computeShader.Dispatch(kernelComp, threadGroups, 1, 1);

            countBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Raw);
            ComputeBuffer.CopyCount(newPointsBuffer, countBuffer, 0);

            int[] countArray = { 0 };
            countBuffer.GetData(countArray);
            int newPointCount = countArray[0];
            int finalPointCount = originalPointCount + newPointCount;

            if (finalPointCount > maxCombinedPoints)
            {
                UnityEngine.Debug.LogError($"�o�b�t�@�I�[�o�[�������o�B �m��: {maxCombinedPoints}, �K�v: {finalPointCount}");
                finalPointCount = maxCombinedPoints;
                newPointCount = maxCombinedPoints - originalPointCount;
            }

            if (newPointCount > 0)
            {
                computeShader.SetInt("_DensityThreshold", originalPointCount);
                computeShader.SetInt("_PointsPerAxis", newPointCount);

                computeShader.SetBuffer(kernelMerge, "_PointsIn", pointsBuffer);
                computeShader.SetBuffer(kernelMerge, "_NewPointsIn", newPointsBuffer);
                computeShader.SetBuffer(kernelMerge, "_PointsOut", finalCombinedBuffer);

                int mergeThreadGroups = Mathf.CeilToInt(finalPointCount / 64.0f);
                computeShader.Dispatch(kernelMerge, mergeThreadGroups, 1, 1);
            }
            else
            {
                if (originalPointCount > 0)
                {
                    computeShader.SetInt("_DensityThreshold", originalPointCount);
                    computeShader.SetBuffer(kernelBlit, "_PointsIn", pointsBuffer);
                    computeShader.SetBuffer(kernelBlit, "_PointsOut", finalCombinedBuffer);

                    int blitThreadGroups = Mathf.CeilToInt(originalPointCount / 64.0f);
                    computeShader.Dispatch(kernelBlit, blitThreadGroups, 1, 1);
                }
            }

            return ReadDataFromGpuBuffer(finalCombinedBuffer, finalPointCount, finalPointCount);
        }
        catch (System.Exception ex)
        {
            UnityEngine.Debug.LogError($"ApplyGPU�G���[: {ex.Message}");
            return null;
        }
        finally
        {
            pointsBuffer?.Release();
            newPointsBuffer?.Release();
            finalCombinedBuffer?.Release();
            countBuffer?.Release();
            gpuVoxelGrid?.Dispose();
        }
    }

    private static PCV_Data ReadDataFromGpuBuffer(ComputeBuffer buffer, int countToRead, int listCapacity)
    {
        var finalVertices = new List<Vector3>(listCapacity);
        var finalColors = new List<Color>(listCapacity);

        if (countToRead > 0)
        {
            var finalPointData = new Point[countToRead];
            buffer.GetData(finalPointData, 0, 0, countToRead);

            for (int i = 0; i < countToRead; i++)
            {
                finalVertices.Add(new Vector3(
                    finalPointData[i].position.x,
                    finalPointData[i].position.y,
                    finalPointData[i].position.z
                ));
                finalColors.Add(finalPointData[i].color);
            }
        }
        return new PCV_Data(finalVertices, finalColors);
    }

    private static PCV_Data ApplyCPU(PCV_Data currentData, VoxelGrid voxelGrid, PCV_Settings settings, Stopwatch stopwatch)
    {
        uint pointsPerAxis = settings.complementationPointsPerAxis;
        if (pointsPerAxis == 0)
        {
            UnityEngine.Debug.LogWarning("complementationPointsPerAxis �� 0 �ȉ��ɐݒ肳��Ă��邽�߁A�������X�L�b�v���܂��B");
            return null;
        }

        bool useRandomPlacement = settings.complementationRandomPlacement;
        uint totalPointsPerVoxel = (pointsPerAxis == 1) ? 1u : (pointsPerAxis * pointsPerAxis);
        string placementMode = useRandomPlacement ? "�����_���z�u" : "�ϓ��z�u";
        UnityEngine.Debug.Log($"���x�⊮���� (CPU) ���J�n���܂��B(臒l: {settings.complementationDensityThreshold}, �ǉ��_: {totalPointsPerVoxel}�_/Voxel, ���[�h: {placementMode})");

        float voxelSize = settings.voxelSize;
        Color pointColor = settings.complementationPointColor;

        var additionalVertices = new List<Vector3>();
        var additionalColors = new List<Color>();

        foreach (var kvp in voxelGrid.Grid)
        {
            if (kvp.Value.Count >= settings.complementationDensityThreshold)
            {
                Vector3Int voxelIndex = kvp.Key;
                float centerX = (voxelIndex.x * voxelSize) + (voxelSize / 2.0f);
                float voxelMinY = voxelIndex.y * voxelSize;
                float voxelMinZ = voxelIndex.z * voxelSize;

                if (useRandomPlacement)
                {
                    float voxelMaxY = voxelMinY + voxelSize;
                    float voxelMaxZ = voxelMinZ + voxelSize;
                    for (uint i = 0; i < totalPointsPerVoxel; i++)
                    {
                        float pointY = UnityEngine.Random.Range(voxelMinY, voxelMaxY);
                        float pointZ = UnityEngine.Random.Range(voxelMinZ, voxelMaxZ);
                        additionalVertices.Add(new Vector3(centerX, pointY, pointZ));
                        additionalColors.Add(pointColor);
                    }
                }
                else
                {
                    if (pointsPerAxis == 1)
                    {
                        float centerY = voxelMinY + (voxelSize / 2.0f);
                        float centerZ = voxelMinZ + (voxelSize / 2.0f);
                        additionalVertices.Add(new Vector3(centerX, centerY, centerZ));
                        additionalColors.Add(pointColor);
                    }
                    else
                    {
                        float step = voxelSize / pointsPerAxis;
                        float initialOffset = step / 2.0f;
                        for (uint y = 0; y < pointsPerAxis; y++)
                        {
                            float pointY = voxelMinY + initialOffset + (step * y);
                            for (uint z = 0; z < pointsPerAxis; z++)
                            {
                                float pointZ = voxelMinZ + initialOffset + (step * z);
                                additionalVertices.Add(new Vector3(centerX, pointY, pointZ));
                                additionalColors.Add(pointColor);
                            }
                        }
                    }
                }
            }
        }

        if (additionalVertices.Count == 0)
        {
            UnityEngine.Debug.LogWarning("臒l�𒴂���L���ȃ{�N�Z����������܂���ł����B�_�͒ǉ�����܂���B");
            return null;
        }

        int finalCount = currentData.PointCount + additionalVertices.Count;
        var combinedVertices = new List<Vector3>(finalCount);
        var combinedColors = new List<Color>(finalCount);

        combinedVertices.AddRange(currentData.Vertices);
        combinedColors.AddRange(currentData.Colors);

        combinedVertices.AddRange(additionalVertices);
        combinedColors.AddRange(additionalColors);

        PCV_Data combinedData = new PCV_Data(combinedVertices, combinedColors);
        stopwatch.Stop();
        UnityEngine.Debug.Log($"���x�⊮���� (CPU) ���������܂����B{additionalVertices.Count} �_���ǉ�����܂����B��������: {stopwatch.ElapsedMilliseconds} ms.");

        return combinedData;
    }
}