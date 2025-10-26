using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Text;

public static class PCV_VoxelCountExporter
{
    public static void Export(VoxelGrid voxelGrid)
    {
        if (voxelGrid == null)
        {
            UnityEngine.Debug.LogError("VoxelGrid��null�ł��BVoxel���̃G�N�X�|�[�g�͎��s�s�\�ł��B");
            return;
        }

        string path = EditorUtility.SaveFilePanel(
            "Voxel���Ƃ̓_�Q����CSV�Ƃ��ĕۑ�",
            "",
            "voxel_counts.csv",
            "csv");

        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        try
        {
            var csv = new StringBuilder();
            csv.AppendLine("VoxelIndex_X,VoxelIndex_Y,VoxelIndex_Z,PointCount");

            foreach (var kvp in voxelGrid.Grid)
            {
                Vector3Int voxelIndex = kvp.Key;
                int pointCount = kvp.Value.Count;
                csv.AppendLine($"{voxelIndex.x},{voxelIndex.y},{voxelIndex.z},{pointCount}");
            }

            File.WriteAllText(path, csv.ToString());
            UnityEngine.Debug.Log($"Voxel���Ƃ̓_�Q��������ɃG�N�X�|�[�g����܂���: {path}");
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError($"Voxel���̃G�N�X�|�[�g�Ɏ��s���܂���: {e.Message}");
        }
    }
}
