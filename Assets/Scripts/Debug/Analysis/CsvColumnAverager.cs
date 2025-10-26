using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public enum FileType
{
    CSV,
    TXT
}

[AddComponentMenu("Tools/Folder Averager")]
public class FolderAverager : MonoBehaviour
{
    [Header("�y�@�\�����z")]
    [Tooltip("�w�肳�ꂽ�t�H���_����CSV�܂���TXT�t�@�C���̕��ϒl���v�Z���A�R���\�[���ɏo�͂��܂��B")]
    [TextArea(2, 4)]
    public string description = "���̃v���_�E���Ńt�@�C���`����I�����A�{�^���������Ď��s���Ă��������B";

    [Header("�y�ݒ荀�ځz")]
    [Tooltip("��������t�@�C���`���iCSV�܂���TXT�j��I�����܂��B")]
    public FileType fileTypeToProcess = FileType.CSV;

    [Tooltip("�t�@�C�����i�[����Ă���t�H���_�̃p�X���w�肵�܂��B")]
    public string folderPath = "Assets/HandTrakingData/Filter";

    [Header("�yTXT�p 臒l�ݒ�z")]
    [Tooltip("TXT�t�@�C����������臒l�t�B���^��L���ɂ��邩�B")]
    public bool useThreshold = false;

    [Tooltip("���̒l��菬�����f�[�^�͌v�Z���珜�O����܂��B")]
    public Vector3 minThreshold = new Vector3(-100f, -100f, -100f);

    [Tooltip("���̒l���傫���f�[�^�͌v�Z���珜�O����܂��B")]
    public Vector3 maxThreshold = new Vector3(100f, 100f, 100f);


    public void CalculateAndLogAverages()
    {
        string searchPattern = (fileTypeToProcess == FileType.CSV) ? "*.csv" : "*.txt";

        if (!Directory.Exists(folderPath))
        {
            UnityEngine.Debug.LogError("�w�肳�ꂽ�t�H���_��������܂���: " + folderPath);
            return;
        }

        string[] files = Directory.GetFiles(folderPath, searchPattern, SearchOption.TopDirectoryOnly);

        if (files.Length == 0)
        {
            UnityEngine.Debug.LogWarning($"�w�肳�ꂽ�t�H���_����{searchPattern}�t�@�C����������܂���B");
            return;
        }

        UnityEngine.Debug.Log($"[{files.Length}]��{fileTypeToProcess}�t�@�C���̏������J�n���܂�...");

        foreach (var path in files)
        {
            try
            {
                switch (fileTypeToProcess)
                {
                    case FileType.CSV:
                        ProcessCsvFile(path);
                        break;
                    case FileType.TXT:
                        ProcessTxtFile(path);
                        break;
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"�t�@�C���̏������ɃG���[���������܂��� ({Path.GetFileName(path)}): {ex.Message}");
            }
        }
        UnityEngine.Debug.Log("���ׂẴt�@�C���̏������������܂����B");
    }

    private void ProcessCsvFile(string path)
    {
        var lines = File.ReadAllLines(path).Skip(1);
        var rows = new List<double[]>();

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var values = line.Split(',');
            if (values.Length < 4) continue;

            rows.Add(new double[] {
                double.Parse(values[0]), double.Parse(values[1]),
                double.Parse(values[2]), double.Parse(values[3])
            });
        }

        if (rows.Count == 0)
        {
            UnityEngine.Debug.LogWarning($"�L���ȃf�[�^�����݂��܂���: {Path.GetFileName(path)}");
            return;
        }

        double avgProcTime = rows.Average(r => r[1]);
        double avgDiscarded = rows.Average(r => r[2]);
        double avgTotal = rows.Average(r => r[3]);
        double avgRatio = rows.Average(r => (r[3] == 0) ? 0 : r[2] / r[3]);

        UnityEngine.Debug.Log($"==== {Path.GetFileName(path)} (CSV) ====");
        UnityEngine.Debug.Log($"ProcessingTime Avg: {avgProcTime}");
        UnityEngine.Debug.Log($"DiscardedCount Avg: {avgDiscarded}");
        UnityEngine.Debug.Log($"TotalCount Avg: {avgTotal}");
        UnityEngine.Debug.Log($"Discarded/Total Avg: {avgRatio}");
    }

    private void ProcessTxtFile(string path)
    {
        var lines = File.ReadAllLines(path);
        var vectors = new List<Vector3>();

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var values = line.Split(',');
            if (values.Length < 3) continue;

            vectors.Add(new Vector3(
                float.Parse(values[0]), float.Parse(values[1]), float.Parse(values[2])
            ));
        }

        int originalCount = vectors.Count;
        List<Vector3> filteredVectors = vectors;

        if (useThreshold)
        {
            filteredVectors = vectors.Where(v =>
                v.x >= minThreshold.x && v.x <= maxThreshold.x &&
                v.y >= minThreshold.y && v.y <= maxThreshold.y &&
                v.z >= minThreshold.z && v.z <= maxThreshold.z
            ).ToList();

            UnityEngine.Debug.Log($"臒l�t�B���^�����O ({Path.GetFileName(path)}): {originalCount}�� �� {filteredVectors.Count}��");
        }

        if (filteredVectors.Count == 0)
        {
            UnityEngine.Debug.LogWarning($"�L���ȃf�[�^�����݂��܂���i�t�B���^��j: {Path.GetFileName(path)}");
            return;
        }

        float avgX = filteredVectors.Average(v => v.x);
        float avgY = filteredVectors.Average(v => v.y);
        float avgZ = filteredVectors.Average(v => v.z);

        UnityEngine.Debug.Log($"==== {Path.GetFileName(path)} (TXT) ====");
        UnityEngine.Debug.Log($"Average X: {avgX}");
        UnityEngine.Debug.Log($"Average Y: {avgY}");
        UnityEngine.Debug.Log($"Average Z: {avgZ}");
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(FolderAverager))]
[CanEditMultipleObjects]
public class FolderAveragerEditor : Editor
{
    // SerializedProperty ���g���ĕϐ����������ƂŁAUndo��Prefab�̏㏑���Ȃǂ��������@�\����
    SerializedProperty fileTypeProp;
    SerializedProperty folderPathProp;
    SerializedProperty useThresholdProp;
    SerializedProperty minThresholdProp;
    SerializedProperty maxThresholdProp;

    void OnEnable()
    {
        // �C���X�y�N�^�[�ŕ\���E�ҏW����v���p�e�B�i�ϐ��j�𖼑O�Ŏ擾
        fileTypeProp = serializedObject.FindProperty("fileTypeToProcess");
        folderPathProp = serializedObject.FindProperty("folderPath");
        useThresholdProp = serializedObject.FindProperty("useThreshold");
        minThresholdProp = serializedObject.FindProperty("minThreshold");
        maxThresholdProp = serializedObject.FindProperty("maxThreshold");
    }
    
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.LabelField("�y�@�\�����z", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("�w�肳�ꂽ�t�H���_����CSV�܂���TXT�t�@�C���̕��ϒl���v�Z���A�R���\�[���ɏo�͂��܂��B", MessageType.Info);
        
        EditorGUILayout.Space(10);
        
        EditorGUILayout.PropertyField(fileTypeProp, new GUIContent("��������t�@�C���`��"));
        EditorGUILayout.PropertyField(folderPathProp, new GUIContent("�t�H���_�̃p�X"));
        
        if (fileTypeProp.enumValueIndex == (int)FileType.TXT)
        {
            EditorGUILayout.Space(10);
            
            EditorGUILayout.PropertyField(useThresholdProp, new GUIContent("臒l���g�p����"));

            if (useThresholdProp.boolValue)
            {
                EditorGUILayout.PropertyField(minThresholdProp, new GUIContent("�ŏ�臒l (X, Y, Z)"));
                EditorGUILayout.PropertyField(maxThresholdProp, new GUIContent("�ő�臒l (X, Y, Z)"));
            }
        }
        
        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space(15);
        GUI.backgroundColor = new Color(0.6f, 0.8f, 1f);

        FolderAverager script = (FolderAverager)target;
        string buttonText = $"{script.fileTypeToProcess} �t�@�C���̕��ϒl���v�Z";

        if (GUILayout.Button(buttonText))
        {
            foreach (var obj in targets)
            {
                FolderAverager s = (FolderAverager)obj;
                s.CalculateAndLogAverages();
            }
        }
        GUI.backgroundColor = Color.white;
    }
}
#endif