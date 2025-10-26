using System;
using System.IO;
using System.Text;
using UnityEngine;

public class PerformanceLogger : IDisposable
{
    private StreamWriter _csvWriter;
    private readonly StringBuilder _csvBuilder = new StringBuilder();
    public bool IsLogging { get; private set; } = false;

    private long _startFrame;
    private long _endFrame;

    public void StartLogging(string fileNamePrefix, bool append = false, long startFrame = 0, long endFrame = long.MaxValue)
    {
        if (IsLogging)
        {
            UnityEngine.Debug.LogWarning("�p�t�H�[�}���X���K�[�͊��Ɏ��s���ł��B");
            return;
        }

        _startFrame = startFrame;
        _endFrame = endFrame;

        try
        {
            string directoryPath = Path.Combine(UnityEngine.Application.dataPath, "HandTrakingData", "Filter");
            Directory.CreateDirectory(directoryPath);

            string fileName;
            if (append)
            {
                fileName = $"{fileNamePrefix}_aggregated.csv";
            }
            else
            {
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                fileName = $"{fileNamePrefix}_{timestamp}.csv";
            }

            string filePath = Path.Combine(directoryPath, fileName);
            bool fileExists = File.Exists(filePath);

            _csvWriter = new StreamWriter(filePath, append, Encoding.UTF8);

            if (!fileExists || !append)
            {
                _csvBuilder.Clear();
                _csvBuilder.Append("Frame,ProcessingTime_ms,DiscardedCount,TotalCount,IsFilterEnabled");
                _csvWriter.WriteLine(_csvBuilder.ToString());
                _csvBuilder.Clear();
            }
            else if (append && fileExists)
            {
                _csvWriter.WriteLine($"--- New Session Started at {DateTime.Now:yyyy-MM-dd HH:mm:ss} ---");
            }

            IsLogging = true;
            UnityEngine.Debug.Log($"�p�t�H�[�}���X���O���J�n���܂����B�o�͐�: {filePath} (�L�^�͈�: {_startFrame}�`{_endFrame}�t���[��)");
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError($"���O�t�@�C���̍쐬�Ɏ��s���܂���: {e.Message}");
            IsLogging = false;
        }
    }

    public void StopLogging()
    {
        if (!IsLogging) return;

        _csvWriter?.Flush();
        _csvWriter?.Dispose();
        _csvWriter = null;
        IsLogging = false;
        UnityEngine.Debug.Log("�p�t�H�[�}���X���O���I�����܂����B");
    }

    public void LogFrame(long frame, double processingTime, long discardedCount, long totalCount, bool isFilterEnabled)
    {
        if (!IsLogging || _csvWriter == null) return;

        if (frame > _endFrame)
        {
            StopLogging();
            return;
        }

        if (frame < _startFrame)
        {
            return;
        }

        _csvBuilder.Append(frame).Append(',');
        _csvBuilder.Append(processingTime.ToString("F4")).Append(',');
        _csvBuilder.Append(discardedCount).Append(',');
        _csvBuilder.Append(totalCount).Append(',');
        _csvBuilder.Append(isFilterEnabled);
        _csvWriter.WriteLine(_csvBuilder.ToString());
        _csvBuilder.Clear();
    }

    public void Dispose()
    {
        StopLogging();
    }
}