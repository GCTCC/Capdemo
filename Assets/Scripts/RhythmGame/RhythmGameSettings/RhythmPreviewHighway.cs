using System;
using UnityEngine;
using UnityEngine.UI;

// 6레인 미리보기. 실제 NoteManager와 같은 Z = judgeZ + (hitTime - ChartTime) * speed를 쓴다.
// 기본 게임 카메라의 원근 투영을 오른쪽 UI 영역에 맞춰 그린다.
[RequireComponent(typeof(CanvasRenderer))]
public class RhythmPreviewHighway : MaskableGraphic
{
    private double songTime;
    private float worldSpeed = 15;
    private double deviceMs = 145.83333;
    private int syncMs;
    private float nearZ, farZ = 19.85f, judgeZ = 1.15f, nearWidth = 5.8f, farWidth = 2.376f;
    private float ghostZ;
    private float ghostUntil;
    private float pressUntil;
    private readonly Vector3 cameraPosition = new Vector3(0, 8, -9.2f);

    public void Configure(float near, float far, float judge, float nearHalfWidth, float farHalfWidth)
    {
        nearZ = near; farZ = far; judgeZ = judge; nearWidth = nearHalfWidth; farWidth = farHalfWidth;
    }

    public void SetFrame(double referenceTime, float speed, double calibrationMs, int offsetMs)
    {
        songTime = referenceTime; worldSpeed = speed; deviceMs = calibrationMs; syncMs = offsetMs;
        SetVerticesDirty();
    }

    public void ResetFeedback() { ghostUntil = pressUntil = 0; SetVerticesDirty(); }

    public void ShowInput(double targetTime, double chartTime)
    {
        ghostZ = judgeZ + (float)(targetTime - chartTime) * worldSpeed;
        ghostUntil = Time.unscaledTime + 0.9f;
        pressUntil = Time.unscaledTime + 0.12f;
        SetVerticesDirty();
    }

    private float HalfWidth(float z)
    {
        return Mathf.Lerp(nearWidth, farWidth, Mathf.Clamp01((z - nearZ) / (farZ - nearZ)));
    }

    private Vector2 RawProject(float x, float z)
    {
        Vector3 forward = (new Vector3(0, 0, 8.2f) - cameraPosition).normalized;
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
        Vector3 up = Vector3.Cross(forward, right);
        Vector3 point = new Vector3(x, 0, z) - cameraPosition;
        float depth = Mathf.Max(0.1f, Vector3.Dot(point, forward));
        return new Vector2(Vector3.Dot(point, right) / depth, Vector3.Dot(point, up) / depth);
    }

    private Vector2 Project(float x, float z)
    {
        Rect r = rectTransform.rect;
        float y0 = RawProject(0, judgeZ).y;
        float y1 = RawProject(0, farZ).y;
        Vector2 p = RawProject(x, z);
        float scaleX = r.width * 0.45f / Mathf.Max(0.001f, Mathf.Abs(RawProject(HalfWidth(judgeZ), judgeZ).x));
        return new Vector2(r.center.x + p.x * scaleX,
            r.yMin + 132 + (p.y - y0) / (y1 - y0) * (r.height - 190));
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect r = rectTransform.rect;
        Quad(vh, new Vector2(r.xMin, r.yMin), new Vector2(r.xMax, r.yMin),
            new Vector2(r.xMin, r.yMax), new Vector2(r.xMax, r.yMax), new Color(0.018f, 0.04f, 0.06f, 1));
        Quad(vh, Project(-nearWidth, nearZ), Project(nearWidth, nearZ),
            Project(-farWidth, farZ), Project(farWidth, farZ), new Color(0.035f, 0.10f, 0.12f, 1));
        for (int i = 0; i <= 6; i++)
        {
            float t = i / 6f;
            Line(vh, Project(Mathf.Lerp(-nearWidth, nearWidth, t), nearZ),
                Project(Mathf.Lerp(-farWidth, farWidth, t), farZ), i == 0 || i == 6 ? 2.5f : 1,
                new Color(0.42f, 0.85f, 0.78f, i == 3 ? 0.6f : 0.26f));
        }
        double time = JudgementTimingSetting.ChartTime(songTime, deviceMs, syncMs);
        double approach = NoteScrollSetting.ApproachSeconds(farZ, judgeZ, worldSpeed);
        long first = Math.Max(0, (long)Math.Floor((time - 1) / RhythmCalibrationSession.BeatInterval));
        long last = (long)Math.Ceiling((time + approach + 1) / RhythmCalibrationSession.BeatInterval);
        for (long beat = first; beat <= last && beat < first + 64; beat++)
        {
            float z = judgeZ + (float)(beat * RhythmCalibrationSession.BeatInterval - time) * worldSpeed;
            if (z < nearZ || z > farZ) continue;
            float w = HalfWidth(z) * 0.77f;
            Vector2 left = Project(-w, z), right = Project(w, z);
            float h = Mathf.Max(7, 15 * (right.x - left.x) / (r.width * 0.9f));
            Quad(vh, left - Vector2.up * h * 0.5f, right - Vector2.up * h * 0.5f,
                left + Vector2.up * h * 0.5f, right + Vector2.up * h * 0.5f, new Color(0.45f, 0.97f, 0.87f, 1));
            Line(vh, left + Vector2.up * h * 0.3f, right + Vector2.up * h * 0.3f, 2, Color.white);
        }
        Vector2 jl = Project(-HalfWidth(judgeZ), judgeZ), jr = Project(HalfWidth(judgeZ), judgeZ);
        Line(vh, jl, jr, 16, new Color(0.35f, 1, 0.85f, 0.13f));
        Line(vh, jl, jr, 3, Time.unscaledTime < pressUntil ? Color.white : new Color(0.65f, 1, 0.91f));
        if (Time.unscaledTime < ghostUntil && ghostZ >= nearZ && ghostZ <= farZ)
            Line(vh, Project(-HalfWidth(ghostZ) * 0.86f, ghostZ), Project(HalfWidth(ghostZ) * 0.86f, ghostZ),
                4, new Color(1, 0.4f, 0.65f, 0.9f));
    }

    private static void Line(VertexHelper vh, Vector2 a, Vector2 b, float thickness, Color color)
    {
        Vector2 normal = new Vector2(-(b - a).y, (b - a).x).normalized * thickness * 0.5f;
        Quad(vh, a - normal, b - normal, a + normal, b + normal, color);
    }
    private static void Quad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color color)
    {
        int k = vh.currentVertCount;
        vh.AddVert(a, color, Vector2.zero); vh.AddVert(b, color, Vector2.zero);
        vh.AddVert(c, color, Vector2.zero); vh.AddVert(d, color, Vector2.zero);
        vh.AddTriangle(k, k + 2, k + 1); vh.AddTriangle(k + 1, k + 2, k + 3);
    }
}
