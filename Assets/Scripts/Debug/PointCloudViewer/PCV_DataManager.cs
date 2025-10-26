using System;
using UnityEngine;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor; // EditorApplication �� AssemblyReloadEvents �̂��߂ɒǉ�
#endif

public class PCV_DataManager : MonoBehaviour
{
    public PCV_Data CurrentData { get; private set; }
    public PCV_SpatialSearch SpatialSearch { get; private set; }
    public event Action<PCV_Data> OnDataUpdated;

    public void LoadAndSetData(FileSettings[] fileSettings, float voxelSize)
    {
        PCV_Data loadedData = PCV_Loader.LoadFromFiles(fileSettings);
        SetData(loadedData, voxelSize);

        if (loadedData != null && loadedData.PointCount > 0)
        {
            UnityEngine.Debug.Log($"�_�Q�� {loadedData.PointCount} �_�ōč\�z����܂����B");
        }
        else
        {
            UnityEngine.Debug.LogWarning("�ǂݍ��ޓ_�Q�f�[�^�����݂��܂���B");
        }
    }

    public void SetData(PCV_Data newData, float voxelSize)
    {
        ReleaseAllBuffers();

        CurrentData = newData;

        if (CurrentData != null && CurrentData.PointCount > 0)
        {
            SpatialSearch = new PCV_SpatialSearch(CurrentData, voxelSize);
            if (SpatialSearch.VoxelGrid != null)
            {
                SpatialSearch.VoxelGrid.SetPointDataCache(CurrentData);
            }
        }
        else
        {
            SpatialSearch = null;
        }

        OnDataUpdated?.Invoke(CurrentData);
    }

    private void OnDestroy()
    {
        ReleaseAllBuffers();
#if UNITY_EDITOR
        AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
#endif
    }

    private void OnDisable()
    {
        ReleaseAllBuffers();
#if UNITY_EDITOR
        AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
#endif
    }

    private void ReleaseAllBuffers()
    {
        if (SpatialSearch != null)
        {
            SpatialSearch.Dispose();
            SpatialSearch = null;
        }
    }

#if UNITY_EDITOR
    private void OnEnable()
    {
        AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private void OnBeforeAssemblyReload()
    {
        ReleaseAllBuffers();
    }

    private void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingPlayMode || state == PlayModeStateChange.EnteredEditMode)
        {
            ReleaseAllBuffers();
        }
    }
#endif
}