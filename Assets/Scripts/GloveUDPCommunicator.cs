using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// KOCCA game glove demo UDP bridge.
/// Receives UTF-8 JSON from the glove on 9001 and sends mode commands to the glove on 9002.
/// </summary>
public class GloveUDPCommunicator : MonoBehaviour
{
    private const string IncomingSchema = "kocca-game-glove-demo-v1";
    private const string CommandSchema = "kocca-game-command-v1";

    [Header("Network")]
    [Tooltip("Unity receive bind address. Use 0.0.0.0 unless you need to pin it to the PC IP.")]
    public string localListenAddress = "0.0.0.0";

    [Tooltip("Unity receive port for glove -> Unity packets.")]
    public int receivePort = 9001;

    [Tooltip("Glove/cRIO IP address.")]
    public string gloveIP = "192.168.0.10";

    [Tooltip("Glove/cRIO command port for Unity -> glove mode packets.")]
    public int commandPort = 9002;

    [Tooltip("Optional Unity PC source IP for mode commands. Leave empty to let Windows choose the route.")]
    public string localCommandAddress = "";

    [Tooltip("Optional Unity PC source port for mode commands. 0 means an ephemeral UDP port.")]
    public int localCommandPort = 0;

    [Tooltip("How many times to repeat a mode command to reduce UDP packet loss.")]
    [Range(1, 10)]
    public int commandRepeatCount = 5;

    [Tooltip("Delay between repeated mode command packets, in seconds.")]
    public float commandRepeatInterval = 0.03f;

    [Header("Gameplay")]
    public Pistol pistol;
    public PlayerStatus playerStatus;
    public Animator handAnimator;

    [Tooltip("Update Trigger/Grip animation from the current glove mode.")]
    public bool updateHandAnimatorFromMode = true;

    [Tooltip("Send the initial mode command once this component starts.")]
    public bool sendInitialModeOnStart = false;

    public string initialMode = "sword";

    [Tooltip("Minimum incoming sword damage_multiplier.")]
    public float minDamageMultiplier = 1f;

    [Tooltip("Maximum incoming sword damage_multiplier.")]
    public float maxDamageMultiplier = 3f;

    [Header("Events")]
    public UnityEvent onGloveShot;

    [Header("Debug")]
    public bool showDebugLog = false;

    public string CurrentMode { get; private set; } = "";
    public int LastShotId => _lastShotId;
    public float CurrentDamageMultiplier => _damageMultiplier;
    public float CurrentTension => _currentTension;

    private readonly Queue<string> _receivedJson = new Queue<string>();
    private readonly object _lock = new object();
    private readonly float[] _fingerPose = new float[5];

    private UdpClient _receiver;
    private UdpClient _sender;
    private Thread _receiveThread;
    private IPEndPoint _gloveEndPoint;
    private Coroutine _sendModeCoroutine;

    private bool _isRunning;
    private bool _hasShotBaseline;
    private bool _modeCommandRequested;
    private int _lastShotId;
    private float _damageMultiplier = 1f;
    private float _currentTension;

    [Serializable]
    public class GloveStatePacket
    {
        public string schema;
        public string mode;
        public float damage_multiplier;
        public int shot_id;
    }

    private void Awake()
    {
        if (playerStatus == null)
        {
            playerStatus = PlayerStatus.Instance;
        }
    }

    private void Start()
    {
        bool receiverStarted = StartReceiver();
        EnsureCommandEndpoint();

        if (sendInitialModeOnStart && !_modeCommandRequested)
        {
            SendModeCommand(initialMode);
        }

        if (receiverStarted)
        {
            Debug.Log($"[Glove] Listening on {localListenAddress}:{receivePort}, commands -> {gloveIP}:{commandPort}");
        }
    }

    private void Update()
    {
        while (TryDequeuePacket(out string json))
        {
            ProcessIncomingJson(json);
        }
    }

    public void SetSwordMode()
    {
        SendModeCommand("sword");
    }

    public void SetGunMode()
    {
        SendModeCommand("gun");
    }

    public void SendModeCommand(string mode)
    {
        mode = NormalizeMode(mode);
        if (string.IsNullOrEmpty(mode))
        {
            Debug.LogWarning("[Glove] Mode command ignored. Use 'sword' or 'gun'.");
            return;
        }

        CurrentMode = mode;
        _modeCommandRequested = true;
        ApplyHandPoseForMode(mode);

        if (showDebugLog)
        {
            Debug.Log($"[Glove] Queue mode command '{mode}' -> {gloveIP}:{commandPort}");
        }

        if (_sendModeCoroutine != null)
        {
            StopCoroutine(_sendModeCoroutine);
        }

        _sendModeCoroutine = StartCoroutine(SendModeCommandRepeated(mode));
    }

    /// <summary>
    /// Legacy helper kept so old call sites still compile. New glove firmware accepts mode JSON only.
    /// </summary>
    public void SendTargets(float[] values)
    {
        if (values == null || values.Length < 5)
        {
            return;
        }

        bool looksLikeSword = values[1] >= 0.3f && values[2] >= 0.3f && values[3] >= 0.3f && values[4] >= 0.3f;
        bool looksLikeGun = values[1] <= 0.1f && values[2] >= 0.7f && values[3] >= 0.7f && values[4] >= 0.7f;

        if (looksLikeGun)
        {
            SendModeCommand("gun");
        }
        else if (looksLikeSword)
        {
            SendModeCommand("sword");
        }
        else
        {
            Debug.LogWarning("[Glove] Raw finger targets are not supported by the JSON protocol. Send sword/gun mode instead.");
        }
    }

    public float GetFingerAngle(int fingerIndex)
    {
        int index = Mathf.Clamp(fingerIndex, 0, _fingerPose.Length - 1);
        return _fingerPose[index];
    }

    private bool StartReceiver()
    {
        IPAddress bindAddress = IPAddress.Any;
        if (!string.IsNullOrEmpty(localListenAddress) && localListenAddress != "0.0.0.0")
        {
            if (!IPAddress.TryParse(localListenAddress, out bindAddress))
            {
                Debug.LogWarning($"[Glove] Invalid listen address '{localListenAddress}', falling back to 0.0.0.0.");
                bindAddress = IPAddress.Any;
            }
        }

        _receiver = new UdpClient();
        try
        {
            _receiver.Client.Bind(new IPEndPoint(bindAddress, receivePort));
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Glove] Could not listen on {localListenAddress}:{receivePort}. Check for another app using the port. {e.Message}");
            _receiver.Close();
            _receiver = null;
            return false;
        }

        _isRunning = true;
        _receiveThread = new Thread(ReceiveLoop) { IsBackground = true };
        _receiveThread.Start();
        return true;
    }

    private bool EnsureCommandEndpoint()
    {
        if (!IPAddress.TryParse(gloveIP, out IPAddress gloveAddress))
        {
            Debug.LogWarning($"[Glove] Invalid glove IP '{gloveIP}'.");
            return false;
        }

        if (_sender == null)
        {
            _sender = CreateCommandSender();
        }
        _gloveEndPoint = new IPEndPoint(gloveAddress, commandPort);
        return true;
    }

    private UdpClient CreateCommandSender()
    {
        UdpClient sender = new UdpClient(AddressFamily.InterNetwork);

        if (string.IsNullOrWhiteSpace(localCommandAddress))
        {
            return sender;
        }

        if (!IPAddress.TryParse(localCommandAddress, out IPAddress localAddress))
        {
            Debug.LogWarning($"[Glove] Invalid local command address '{localCommandAddress}', using the default route.");
            return sender;
        }

        try
        {
            sender.Client.Bind(new IPEndPoint(localAddress, Mathf.Max(0, localCommandPort)));
            if (showDebugLog)
            {
                Debug.Log($"[Glove] Command sender bound to {sender.Client.LocalEndPoint}");
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Glove] Could not bind command sender to {localCommandAddress}:{localCommandPort}. Using the default route. {e.Message}");
        }

        return sender;
    }

    private IEnumerator SendModeCommandRepeated(string mode)
    {
        int repeatCount = Mathf.Max(1, commandRepeatCount);
        float interval = Mathf.Max(0f, commandRepeatInterval);

        for (int i = 0; i < repeatCount; i++)
        {
            SendModeCommandOnce(mode);
            if (i < repeatCount - 1 && interval > 0f)
            {
                yield return new WaitForSeconds(interval);
            }
        }

        _sendModeCoroutine = null;
    }

    private void SendModeCommandOnce(string mode)
    {
        if (!EnsureCommandEndpoint())
        {
            return;
        }

        string json = "{\"schema\":\"" + CommandSchema + "\",\"mode\":\"" + mode + "\"}";
        byte[] bytes = Encoding.UTF8.GetBytes(json);

        try
        {
            _sender.Send(bytes, bytes.Length, _gloveEndPoint);
            if (showDebugLog)
            {
                Debug.Log($"[Glove] Sent mode command {json} from {_sender.Client.LocalEndPoint} to {_gloveEndPoint}");
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Glove] Mode command send failed: {e.Message}");
        }
    }

    private void ReceiveLoop()
    {
        IPEndPoint remote = new IPEndPoint(IPAddress.Any, 0);

        while (_isRunning)
        {
            try
            {
                byte[] data = _receiver.Receive(ref remote);
                string json = Encoding.UTF8.GetString(data);

                lock (_lock)
                {
                    _receivedJson.Enqueue(json);
                }
            }
            catch (SocketException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Glove] Receive error: {e.Message}");
            }
        }
    }

    private bool TryDequeuePacket(out string json)
    {
        lock (_lock)
        {
            if (_receivedJson.Count == 0)
            {
                json = null;
                return false;
            }

            json = _receivedJson.Dequeue();
            return true;
        }
    }

    private void ProcessIncomingJson(string json)
    {
        GloveStatePacket packet;

        try
        {
            packet = JsonUtility.FromJson<GloveStatePacket>(json);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Glove] Invalid JSON packet: {e.Message}");
            return;
        }

        if (packet == null || string.IsNullOrEmpty(packet.mode))
        {
            Debug.LogWarning($"[Glove] Packet missing mode: {json}");
            return;
        }

        if (!string.IsNullOrEmpty(packet.schema) && packet.schema != IncomingSchema)
        {
            Debug.LogWarning($"[Glove] Schema mismatch: {packet.schema}");
            return;
        }

        string mode = NormalizeMode(packet.mode);
        if (string.IsNullOrEmpty(mode))
        {
            Debug.LogWarning($"[Glove] Unknown mode: {packet.mode}");
            return;
        }

        CurrentMode = mode;
        ApplyHandPoseForMode(mode);

        if (mode == "sword")
        {
            ApplySwordState(packet.damage_multiplier);
        }
        else
        {
            ApplyGunState(packet.shot_id);
        }

        if (showDebugLog)
        {
            Debug.Log($"[Glove] Received: {json}");
        }
    }

    private void ApplySwordState(float damageMultiplier)
    {
        if (maxDamageMultiplier <= minDamageMultiplier)
        {
            maxDamageMultiplier = minDamageMultiplier + 0.01f;
        }

        float clampedMultiplier = Mathf.Clamp(damageMultiplier <= 0f ? minDamageMultiplier : damageMultiplier, minDamageMultiplier, maxDamageMultiplier);
        float tension = Mathf.InverseLerp(minDamageMultiplier, maxDamageMultiplier, clampedMultiplier);

        _damageMultiplier = clampedMultiplier;
        _currentTension = tension;

        if (playerStatus == null)
        {
            playerStatus = PlayerStatus.Instance;
        }

        if (playerStatus != null)
        {
            playerStatus.currentTension = tension;
        }
    }

    private void ApplyGunState(int shotId)
    {
        if (!_hasShotBaseline)
        {
            // The glove server can already be running, so the first shot_id only establishes the baseline.
            _lastShotId = shotId;
            _hasShotBaseline = true;

            if (showDebugLog)
            {
                Debug.Log($"[Glove] Shot baseline set: {shotId}");
            }

            return;
        }

        if (shotId <= _lastShotId)
        {
            return;
        }

        // Fire once for a newer shot_id, even if ids jumped because Unity missed packets.
        _lastShotId = shotId;

        if (pistol != null)
        {
            pistol.FireFromGlove();
        }
        else
        {
            Debug.LogWarning("[Glove] Shot received, but no Pistol is assigned.");
        }

        onGloveShot?.Invoke();
    }

    private void ApplyHandPoseForMode(string mode)
    {
        if (mode == "sword")
        {
            SetFingerPose(0f, 0.6f, 0.6f, 0.6f, 0.6f);
        }
        else if (mode == "gun")
        {
            SetFingerPose(0f, 0f, 0.95f, 0.95f, 0.95f);
        }
    }

    private void SetFingerPose(float thumb, float index, float middle, float ring, float pinky)
    {
        _fingerPose[0] = thumb;
        _fingerPose[1] = index;
        _fingerPose[2] = middle;
        _fingerPose[3] = ring;
        _fingerPose[4] = pinky;

        if (!updateHandAnimatorFromMode || handAnimator == null)
        {
            return;
        }

        handAnimator.SetFloat("Trigger", _fingerPose[1]);
        handAnimator.SetFloat("Grip", (_fingerPose[2] + _fingerPose[3] + _fingerPose[4]) / 3f);
    }

    private string NormalizeMode(string mode)
    {
        if (string.IsNullOrEmpty(mode))
        {
            return null;
        }

        mode = mode.Trim().ToLowerInvariant();
        return mode == "sword" || mode == "gun" ? mode : null;
    }

    private void OnDestroy()
    {
        _isRunning = false;

        if (_sendModeCoroutine != null)
        {
            StopCoroutine(_sendModeCoroutine);
            _sendModeCoroutine = null;
        }

        _receiver?.Close();
        _sender?.Close();

        if (_receiveThread != null && _receiveThread.IsAlive)
        {
            _receiveThread.Join(100);
        }
    }
}
