using UnityEngine;
using UnityEngine.VR.WSA.Input;
using UnityEngine.VR.WSA.WebCam;
using System.Linq;
using System.Collections.Generic;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;

public class GazeGestureManager : MonoBehaviour
{
	public static GazeGestureManager Instance { get; private set; }

    GestureRecognizer recognizer;

	Resolution cameraResolution;

	PhotoCapture photoCaptureObject = null;

	Vector3 cameraPosition;
	Quaternion cameraRotation;

	bool _busy = false;

	public GameObject textPrefab;
	public GameObject status;
	public GameObject framePrefab;

	public string faceApiUrl = "http://faceapi.us.to/";
    const string uriBase = "https://westcentralus.api.cognitive.microsoft.com/face/v1.0/detect";
    const string uriBase2 = "https://westcentralus.api.cognitive.microsoft.com/face/v1.0/verify";
    public static string faceID;
    public static string verifyResponse;
    public static string personID;
    public static string confidence;

    void OnPhotoCaptureCreated(PhotoCapture captureObject)
	{
		photoCaptureObject = captureObject;

		cameraResolution = PhotoCapture.SupportedResolutions.OrderByDescending((res) => res.width * res.height).First();

		CameraParameters c = new CameraParameters();
		c.hologramOpacity = 0.0f;
		c.cameraResolutionWidth = cameraResolution.width;
		c.cameraResolutionHeight = cameraResolution.height;
		c.pixelFormat = CapturePixelFormat.PNG;

		captureObject.StartPhotoModeAsync(c, OnPhotoModeStarted);
	}

	private void OnPhotoModeStarted(PhotoCapture.PhotoCaptureResult result)
	{
		if (result.success)
		{
			Debug.Log("Camera ready");
			photoCaptureObject.TakePhotoAsync(OnCapturedPhotoToMemory);
		}
		else
		{
			Debug.LogError("Unable to start photo mode!");
			_busy = false;
		}
	}
    /// <summary>
    /// Gets the analysis of the specified image file by using the Computer Vision REST API.
    /// </summary>
    /// <param name="imageFilePath">The image file.</param>
    static async void MakeAnalysisRequest(byte[] imageData)
    {
        HttpClient client = new HttpClient();

        // Request headers.
        client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", "b7f95616881141269d7daa80123a15c4");

        // Request parameters. A third optional parameter is "details".
        string requestParameters = "returnFaceId=true&returnFaceLandmarks=false&returnFaceAttributes=age,gender,headPose,smile,facialHair,glasses,emotion,hair,makeup,occlusion,accessories,blur,exposure,noise";

        // Assemble the URI for the REST API Call.
        string uri = uriBase + "?" + requestParameters;

        HttpResponseMessage response;


        using (ByteArrayContent content = new ByteArrayContent(imageData))
        {
            // This example uses content type "application/octet-stream".
            // The other content types you can use are "application/json" and "multipart/form-data".
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

            // Execute the REST API call.
            response = await client.PostAsync(uri, content);

            // Get the JSON response.
            faceID = await response.Content.ReadAsStringAsync(); // string
        }
    }
    static async void MakeVerifyRequest()
    {
        HttpClient client = new HttpClient();

        // Request headers.
        client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", "b7f95616881141269d7daa80123a15c4");
        // Assemble the URI for the REST API Call.
        string uri2 = uriBase2;

        HttpResponseMessage response;

        JSONObject j = new JSONObject(faceID);

        personID = j["faceId"].ToString();

        string chrisID = "ab25e0a1-1334-4b55-848c-5371519b64e3";

        byte[] byteData = Encoding.UTF8.GetBytes("{faceId1:" + personID + ", faceId2:" + chrisID + ",}" );

        using (var content = new ByteArrayContent(byteData))
        {
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            response = await client.PostAsync(uri2, content);

            verifyResponse = await response.Content.ReadAsStringAsync(); // string

        }

        JSONObject verifiedJSON = new JSONObject(verifyResponse);

        confidence = verifiedJSON["confidence"].ToString();

    }
    
    IEnumerator<object> PostToFaceAPI(byte[] imageData, Matrix4x4 cameraToWorldMatrix, Matrix4x4 pixelToCameraMatrix) {

        MakeAnalysisRequest(imageData);
        MakeVerifyRequest();

        WWW www = new WWW(faceApiUrl, imageData);
        yield return www;
        string responseString = www.text;
        Debug.Log(responseString);
        JSONObject j = new JSONObject(responseString);
        var existing = GameObject.FindGameObjectsWithTag("faceText");

        foreach (var go in existing)
        {
            Destroy(go);
        }

        existing = GameObject.FindGameObjectsWithTag("faceBounds");

        foreach (var go in existing)
        {
            Destroy(go);
        }

        if (j.list.Count == 0)
        {
            status.GetComponent<TextMesh>().text = "no faces found";
            yield break;
        }
        else
        {
            status.SetActive(false);
        }

        foreach (var result in j.list)
        {
            GameObject txtObject = (GameObject)Instantiate(textPrefab);
            TextMesh txtMesh = txtObject.GetComponent<TextMesh>();
            var r = result["kairos"];
            float top = -(r["topLeftY"].f / cameraResolution.height - .5f);
            float left = r["topLeftX"].f / cameraResolution.width - .5f;
            float width = r["width"].f / cameraResolution.width;
            float height = r["height"].f / cameraResolution.height;

            GameObject faceBounds = (GameObject)Instantiate(framePrefab);
            faceBounds.transform.position = cameraToWorldMatrix.MultiplyPoint3x4(pixelToCameraMatrix.MultiplyPoint3x4(new Vector3(left + width / 2, top, 0)));
            faceBounds.transform.rotation = cameraRotation;
            Vector3 scale = pixelToCameraMatrix.MultiplyPoint3x4(new Vector3(width, height, 0));
            scale.z = .1f;
            faceBounds.transform.localScale = scale;
            faceBounds.tag = "faceBounds";
            Debug.Log(string.Format("{0},{1} translates to {2},{3}", left, top, faceBounds.transform.position.x, faceBounds.transform.position.y));

            Vector3 origin = cameraToWorldMatrix.MultiplyPoint3x4(pixelToCameraMatrix.MultiplyPoint3x4(new Vector3(left + width + .1f, top, 0)));
            txtObject.transform.position = origin;
            txtObject.transform.rotation = cameraRotation;
            txtObject.tag = "faceText";
            if (j.list.Count > 1)
            {
                txtObject.transform.localScale /= 2;
            }

            //txtMesh.text = "Name: Chris Hoang\n Age: 19\n School: University of Michigan\n Interests: Tennis, Piano, Mr. Robot\n";
            txtMesh.text = faceID;
        }
}

    

	void OnCapturedPhotoToMemory(PhotoCapture.PhotoCaptureResult result, PhotoCaptureFrame photoCaptureFrame)
	{
		if (result.success)
		{
			Debug.Log("photo captured");
			List<byte> imageBufferList = new List<byte>();
			// Copy the raw IMFMediaBuffer data into our empty byte list.
			photoCaptureFrame.CopyRawImageDataIntoBuffer(imageBufferList);

			var cameraToWorldMatrix = new Matrix4x4();
			photoCaptureFrame.TryGetCameraToWorldMatrix(out cameraToWorldMatrix);
			
			cameraPosition = cameraToWorldMatrix.MultiplyPoint3x4(new Vector3(0,0,-1));
			cameraRotation = Quaternion.LookRotation(-cameraToWorldMatrix.GetColumn(2), cameraToWorldMatrix.GetColumn(1));

			Matrix4x4 projectionMatrix;
			photoCaptureFrame.TryGetProjectionMatrix(Camera.main.nearClipPlane, Camera.main.farClipPlane, out projectionMatrix);
			Matrix4x4 pixelToCameraMatrix = projectionMatrix.inverse;

			status.GetComponent<TextMesh>().text = "photo captured, processing...";
			status.transform.position = cameraPosition;
			status.transform.rotation = cameraRotation;

            //MakeAnalysisRequest(imageBufferList.ToArray());
			StartCoroutine(PostToFaceAPI(imageBufferList.ToArray(), cameraToWorldMatrix, pixelToCameraMatrix));
			//StartCoroutine(PostToFaceAPI(System.IO.File.ReadAllBytes("nyou045.png"), cameraToWorldMatrix, pixelToCameraMatrix));
		}
		photoCaptureObject.StopPhotoModeAsync(OnStoppedPhotoMode);
	}

	void OnStoppedPhotoMode(PhotoCapture.PhotoCaptureResult result)
	{
		photoCaptureObject.Dispose();
		photoCaptureObject = null;
		_busy = false;
	}

	// Use this for initialization
	void Awake()
	{
		Instance = this;

		// Set up a GestureRecognizer to detect Select gestures.
		recognizer = new GestureRecognizer();
		recognizer.TappedEvent += (source, tapCount, ray) =>
		{
			Debug.Log("tap");
			if (!_busy)
			{
				_busy = true;
				status.GetComponent<TextMesh>().text = "taking photo...";
				status.SetActive(true);
				PhotoCapture.CreateAsync(false, OnPhotoCaptureCreated);
			} else
			{
				status.GetComponent<TextMesh>().text = "busy...";
				status.SetActive(true);
			}
		};
		recognizer.StartCapturingGestures();
		status.GetComponent<TextMesh>().text = "taking photo...";
		_busy = true;

		PhotoCapture.CreateAsync(false, OnPhotoCaptureCreated);
	}
}

internal class Task<T>
{
}