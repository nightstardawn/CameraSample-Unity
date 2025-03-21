using System;
using System.Buffers;
using System.Runtime.InteropServices;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YVR.Core;

namespace YVR.Enterprise.Camera.Samples
{
    public class VSTCameraControl : MonoBehaviour
    {
        private static ArrayPool<byte> s_ArrayPool;

        [SerializeField] private RawImage m_LeftImage;
        [SerializeField] private RawImage m_RightImage;
        [SerializeField] private TMP_Dropdown m_FrequencyDropdown;
        [SerializeField] private TMP_Dropdown m_ResolutionDropdown;
        [SerializeField] private TMP_Dropdown m_FormatDropdown;
        [SerializeField] private TMP_Dropdown m_SourceDropdown;
        [SerializeField] private TextMeshProUGUI m_CameraData;

        private VSTCameraFrequencyType m_FrequencyType;
        private VSTCameraResolutionType m_ResolutionType;
        private VSTCameraFormatType m_FormatType;
        private VSTCameraSourceType m_SourceType;

        private void Start()
        {
            YVRManager.instance.hmdManager.SetPassthrough(true);
            s_ArrayPool = ArrayPool<byte>.Shared;

            m_FrequencyType = (VSTCameraFrequencyType) m_FrequencyDropdown.value;
            SetVSTCameraFrequency();
            m_FrequencyDropdown.onValueChanged.AddListener(value =>
            {
                m_FrequencyType = (VSTCameraFrequencyType) value;
                SetVSTCameraFrequency();
            });

            m_ResolutionType = (VSTCameraResolutionType) m_ResolutionDropdown.value;
            SetVSTCameraResolution();
            m_ResolutionDropdown.onValueChanged.AddListener(value =>
            {
                m_ResolutionType = (VSTCameraResolutionType) value;
                SetVSTCameraResolution();
            });

            m_FormatType = (VSTCameraFormatType) m_FormatDropdown.value;
            SetVSTCameraFormat();
            m_FormatDropdown.onValueChanged.AddListener(value =>
            {
                m_FormatType = (VSTCameraFormatType) value;
                SetVSTCameraFormat();
            });

            m_SourceType = (VSTCameraSourceType) m_SourceDropdown.value;
            SetVSTCameraOutputSource();
            m_SourceDropdown.onValueChanged.AddListener(value =>
            {
                m_SourceType = (VSTCameraSourceType) value;
                SetVSTCameraOutputSource();
            });

            RefreshVSTCameraInfo();
        }

        public void SetVSTCameraFrequency() { YVRVSTCameraPlugin.SetVSTCameraFrequency(m_FrequencyType); }

        public void RefreshVSTCameraInfo()
        {
            VSTCameraFrequencyType freqType = default;
            VSTCameraResolutionType resolution = default;
            VSTCameraFormatType formatType = default;
            VSTCameraSourceType sourceType = default;
            VSTCameraIntrinsicExtrinsicData data = default;

            YVRVSTCameraPlugin.GetVSTCameraFrequency(ref freqType);
            YVRVSTCameraPlugin.GetVSTCameraResolution(ref resolution);
            YVRVSTCameraPlugin.GetVSTCameraFormat(ref formatType);
            YVRVSTCameraPlugin.GetVSTCameraOutputSource(ref sourceType);
            YVRVSTCameraPlugin.GetVSTCameraIntrinsicExtrinsic(YVREyeNumberType.LeftEye, ref data);

            string text = $"Frequency: {freqType}\n" +
                          $"Resolution: {resolution}\n" +
                          $"Format: {formatType}\n" +
                          $"Source: {sourceType}\n" +
                          $"Intrinsic: fx:{data.fx:f1}, fy:{data.fy:f1} \n" +
                          $"           cx:{data.cx:f1}, cy:{data.cy:f1}\n" +
                          $"Extrinsic: Position(x:{data.x:f1}, y:{data.y:f1}, z:{data.z:f1})\n" +
                          $"Rotation(w:{data.rw:f1}, x:{data.rx:f1}, y:{data.ry:f1}, z:{data.rz:f1})";
            m_CameraData.text = text;
        }

        private void SetVSTCameraResolution() { YVRVSTCameraPlugin.SetVSTCameraResolution(m_ResolutionType); }

        private void SetVSTCameraFormat() { YVRVSTCameraPlugin.SetVSTCameraFormat(m_FormatType); }

        private void SetVSTCameraOutputSource()
        {
            Debug.Log($"sss set source is {m_SourceType}");
            YVRVSTCameraPlugin.SetVSTCameraOutputSource(m_SourceType);
        }

        public void OpenVSTCamera() { YVRVSTCameraPlugin.OpenVSTCamera(); }

        public void CloseVSTCamera() { YVRVSTCameraPlugin.CloseVSTCamera(); }

        public void AcquireVSTCameraFrame()
        {
            byte[][] frameBytes = new byte[2][];

            m_LeftImage.texture = null;
            m_RightImage.texture = null;
            VSTCameraFrameData frameData = default;
            YVRVSTCameraPlugin.AcquireVSTCameraFrame(ref frameData);
            for (int i = 0; i < frameData.cameraFrameItem.data.Length; i++)
            {
                if (frameData.cameraFrameItem.data[i] == IntPtr.Zero) continue;
                byte[] data = s_ArrayPool.Rent(frameData.cameraFrameItem.dataSize);
                Marshal.Copy(frameData.cameraFrameItem.data[i], data, 0, frameData.cameraFrameItem.dataSize);
                frameBytes[i] = data;
            }

            if (frameData.cameraFrameItem.data[0] != IntPtr.Zero)
            {
                if (m_LeftImage.texture != null) Destroy(m_LeftImage.texture);

                Texture2D texture2DLeft = LoadNV21Image(frameBytes[0], frameData.cameraFrameItem.width,
                                                        frameData.cameraFrameItem.height);
                m_LeftImage.texture = texture2DLeft;
            }

            if (frameData.cameraFrameItem.data[1] != IntPtr.Zero)
            {
                if (m_RightImage.texture != null) Destroy(m_RightImage.texture);

                Texture2D texture2DRight = LoadNV21Image(frameBytes[1], frameData.cameraFrameItem.width,
                                                         frameData.cameraFrameItem.height);
                m_RightImage.texture = texture2DRight;
            }

            s_ArrayPool.Return(frameBytes[0]);
            s_ArrayPool.Return(frameBytes[1]);
        }

        private Texture2D LoadNV21Image(byte[] nv21Data, int width, int height)
        {
            byte[] rgbData = null;
            ConvertNV21ToRGB(ref rgbData, nv21Data, width, height);
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            texture.LoadRawTextureData(rgbData);
            texture.Apply();
            return texture;
        }

        private static void ConvertNV21ToRGB(ref byte[] rgbData, byte[] nv21Data, int width, int height)
        {
            int frameSize = width * height;

            if (rgbData == null || frameSize != rgbData.Length)
            {
                rgbData = new byte[frameSize * 3];
            }

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int yIndex = y * width + x;
                    int uvIndex = frameSize + (y / 2) * width + (x & ~1);

                    int yData = nv21Data[yIndex] & 0xff;
                    int vData = nv21Data[uvIndex] & 0xff;
                    int uData = nv21Data[uvIndex + 1] & 0xff;

                    yData = yData < 16 ? 16 : yData;

                    int r = (int) (1.164f * (yData - 16) + 1.596f * (vData - 128));
                    int g = (int) (1.164f * (yData - 16) - 0.813f * (vData - 128) - 0.391f * (uData - 128));
                    int b = (int) (1.164f * (yData - 16) + 2.018f * (uData - 128));

                    r = r < 0 ? 0 : (r > 255 ? 255 : r);
                    g = g < 0 ? 0 : (g > 255 ? 255 : g);
                    b = b < 0 ? 0 : (b > 255 ? 255 : b);

                    int rgbIndex = yIndex * 3;
                    rgbData[rgbIndex] = (byte) r;
                    rgbData[rgbIndex + 1] = (byte) g;
                    rgbData[rgbIndex + 2] = (byte) b;
                }
            }
        }
    }
}