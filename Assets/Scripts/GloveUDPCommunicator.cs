using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

/// <summary>
/// 오른손 공압장갑 UDP 통신 컴포넌트.
/// 수신 패킷: float 5개 (Thumb, Index, Middle, Ring, Pinky) 각 0.0~1.0, little-endian binary
/// 송신 패킷: 동일 포맷으로 목표 각도 전송
/// </summary>
public class GloveUDPCommunicator : MonoBehaviour
{
    [Header("Network")]
    [Tooltip("장갑 장치의 IP")]
    public string gloveIP = "192.168.0.100";

    [Tooltip("Unity가 수신할 포트 (장갑 → Unity)")]
    public int receivePort = 5005;

    [Tooltip("장갑으로 송신할 포트 (Unity → 장갑)")]
    public int sendPort = 5006;

    [Header("Hand Animation")]
    public Animator handAnimator;

    [Header("Debug")]
    public bool showDebugLog = false;

    // 수신 데이터 (스레드 → 메인)
    private float[] _received = new float[5]; // Thumb, Index, Middle, Ring, Pinky
    private bool _hasNewData = false;
    private readonly object _lock = new object();

    // 송신 목표값
    private float[] _targets = new float[5];

    private UdpClient _receiver;
    private UdpClient _sender;
    private Thread _receiveThread;
    private IPEndPoint _gloveEndPoint;

    private void Start()
    {
        _gloveEndPoint = new IPEndPoint(IPAddress.Parse(gloveIP), sendPort);

        _receiver = new UdpClient(receivePort);
        _sender   = new UdpClient();

        _receiveThread = new Thread(ReceiveLoop) { IsBackground = true };
        _receiveThread.Start();

        Debug.Log($"[Glove] UDP 수신:{receivePort} / 송신:{sendPort} ({gloveIP})");
    }

    private void Update()
    {
        // 수신 데이터를 Animator에 반영
        float[] data;
        bool hasNew;
        lock (_lock)
        {
            data   = (float[])_received.Clone();
            hasNew = _hasNewData;
            _hasNewData = false;
        }

        if (!hasNew) return;

        float index = data[1];                              // Index  → Trigger
        float grip  = (data[2] + data[3] + data[4]) / 3f; // Middle+Ring+Pinky 평균 → Grip

        handAnimator.SetFloat("Trigger", index);
        handAnimator.SetFloat("Grip",    grip);

        if (showDebugLog)
            Debug.Log($"[Glove] T:{data[0]:F2} I:{data[1]:F2} M:{data[2]:F2} R:{data[3]:F2} P:{data[4]:F2}");
    }

    /// <summary>
    /// 장갑에 목표 각도 송신 (외부에서 호출 가능)
    /// values: float[5] 각 0.0~1.0
    /// </summary>
    public void SendTargets(float[] values)
    {
        if (values == null || values.Length < 5) return;

        byte[] packet = new byte[5 * 4];
        for (int i = 0; i < 5; i++)
            Buffer.BlockCopy(BitConverter.GetBytes(values[i]), 0, packet, i * 4, 4);

        try { _sender.Send(packet, packet.Length, _gloveEndPoint); }
        catch (Exception e) { Debug.LogWarning($"[Glove] 송신 실패: {e.Message}"); }
    }

    /// <summary>현재 수신된 손가락 각도 반환 (0=Thumb ~ 4=Pinky)</summary>
    public float GetFingerAngle(int fingerIndex)
    {
        lock (_lock) { return _received[Mathf.Clamp(fingerIndex, 0, 4)]; }
    }

    // ── 수신 루프 (별도 스레드) ──────────────────────────────
    private void ReceiveLoop()
    {
        IPEndPoint remote = new IPEndPoint(IPAddress.Any, 0);
        while (true)
        {
            try
            {
                byte[] data = _receiver.Receive(ref remote);
                if (data.Length < 5 * 4) continue;

                float[] angles = new float[5];
                for (int i = 0; i < 5; i++)
                    angles[i] = BitConverter.ToSingle(data, i * 4);

                lock (_lock)
                {
                    Array.Copy(angles, _received, 5);
                    _hasNewData = true;
                }
            }
            catch (SocketException) { break; } // 소켓 닫힐 때 정상 종료
            catch (Exception e)     { Debug.LogWarning($"[Glove] 수신 오류: {e.Message}"); }
        }
    }

    private void OnDestroy()
    {
        _receiveThread?.Abort();
        _receiver?.Close();
        _sender?.Close();
    }
}
