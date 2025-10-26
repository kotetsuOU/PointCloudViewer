using UnityEngine;
using System.IO;
using System.Collections;

public class CameraCapture : MonoBehaviour
{
    [Header("Common Settings")]
    public UnityEngine.Camera targetCamera;
    public int captureWidth = 2560;
    public int captureHeight = 1440;

    [Header("Single Capture Settings")]
    public string singleCaptureFolder = "HandTrakingData/RecordedViewPointPicture/Pictures";

    [Header("Video Recording Settings")]
    public int frameRate = 30;
    public string videoFramesFolder = "HandTrakingData/RecordedViewPointPicture/VideoFrames";

    [Header("Frame Control")]
    [Tooltip("�^����J�n����t���[���ԍ� (�J�E���g��0����)�B")]
    public int startFrame = 0;

    [Tooltip("�^����I������t���[���ԍ� (���̃t���[���̃L���v�`���͎��s����Ȃ�)�B")]
    public int endFrame = 300;

    [Header("Automation Options")]
    [Tooltip("�Q�[���J�n���Ɏ����Ř^����J�n���܂��B")]
    public bool autoStartRecordingOnPlay = false;

    [Tooltip("���̃L�[���������ƂŘ^��̊J�n/��~���g�O�����܂��B")]
    public UnityEngine.KeyCode toggleRecordingKey = UnityEngine.KeyCode.R;

    private bool isRecording = false;
    private string currentVideoFolderPath;
    private int frameCount = 0;

    void Start()
    {
        if (autoStartRecordingOnPlay)
        {
            StartRecording();
        }
    }

    void Update()
    {
        if (UnityEngine.Input.GetKeyDown(toggleRecordingKey))
        {
            if (isRecording)
            {
                StopRecording();
            }
            else
            {
                StartRecording();
            }
        }
    }

    public void Capture()
    {
        string directoryPath = System.IO.Path.Combine(UnityEngine.Application.dataPath, singleCaptureFolder);
        if (!System.IO.Directory.Exists(directoryPath))
        {
            System.IO.Directory.CreateDirectory(directoryPath);
        }

        string fileName = string.Format("capture_{0}.png", System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss"));
        string filePath = System.IO.Path.Combine(directoryPath, fileName);

        SaveFrameToFile(filePath);
        UnityEngine.Debug.Log(string.Format("�L���v�`����ۑ����܂���: {0}", filePath));
    }

    public void StartRecording()
    {
        if (isRecording)
        {
            UnityEngine.Debug.LogWarning("���ɘ^�悪�J�n����Ă��܂��B");
            return;
        }

        if (endFrame <= startFrame)
        {
            UnityEngine.Debug.LogError(string.Format("�I���t���[�� ({0}) �͊J�n�t���[�� ({1}) ���傫���ݒ肵�Ă��������B", endFrame, startFrame));
            return;
        }

        isRecording = true;
        frameCount = 0;

        string timeStamp = System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        currentVideoFolderPath = System.IO.Path.Combine(UnityEngine.Application.dataPath, videoFramesFolder, timeStamp);

        if (!System.IO.Directory.Exists(currentVideoFolderPath))
        {
            System.IO.Directory.CreateDirectory(currentVideoFolderPath);
        }

        StartCoroutine(RecordFrames());
        UnityEngine.Debug.Log(string.Format("�^����J�n���܂����B�J�E���g�J�n: 0�A�L���v�`���͈�: {0}�`{1} (�t���[��{1}�͏���)�A�ۑ���: {2}", startFrame, endFrame, currentVideoFolderPath));
    }

    public void StopRecording()
    {
        if (!isRecording)
        {
            return;
        }

        isRecording = false;
        UnityEngine.Debug.Log("�^����~���܂����B���v�t���[����: " + frameCount);
    }

    private System.Collections.IEnumerator RecordFrames()
    {
        float frameDuration = 1f / frameRate;

        while (isRecording)
        {
            if (frameCount >= endFrame)
            {
                StopRecording();
                yield break;
            }

            if (frameCount >= startFrame && frameCount < endFrame)
            {
                string filePath = System.IO.Path.Combine(currentVideoFolderPath, $"frame_{frameCount:D5}.png");
                SaveFrameToFile(filePath);
            }

            frameCount++;

            yield return new UnityEngine.WaitForSeconds(frameDuration);
        }
    }

    private void SaveFrameToFile(string filePath)
    {
        UnityEngine.RenderTexture rt = new UnityEngine.RenderTexture(captureWidth, captureHeight, 24);
        targetCamera.targetTexture = rt;

        UnityEngine.Texture2D screenShot = new UnityEngine.Texture2D(captureWidth, captureHeight, UnityEngine.TextureFormat.RGB24, false);
        targetCamera.Render();

        UnityEngine.RenderTexture.active = rt;
        screenShot.ReadPixels(new UnityEngine.Rect(0, 0, captureWidth, captureHeight), 0, 0);

        targetCamera.targetTexture = null;
        UnityEngine.RenderTexture.active = null;
        UnityEngine.Object.Destroy(rt);

        byte[] bytes = screenShot.EncodeToPNG();
        System.IO.File.WriteAllBytes(filePath, bytes);

        UnityEngine.Object.Destroy(screenShot);
    }
}