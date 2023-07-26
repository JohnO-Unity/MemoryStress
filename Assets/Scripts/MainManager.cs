using System.Text;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MainManager : MonoBehaviour {
	public Text textStatus;
	public Text textVersion;
	public Text textLastRunInfo;
	public Text textAllocationStatus;
	public Text textAllocatedForGraphics;
	public Button buttonStartMemTest;
	public Button buttonPauseMemTest;
	public InputField inputDelayTime;
	public InputField inputTextureSize;
	public InputField inputMemEstimate;
	public Text headerInputSize;
	public Toggle checkboxAllocateTextures;
	public Toggle checkboxAutopauseOnLowMemory;

	private bool allocateTextureMem = true;
	private float delayAllocationTime = 0.1f;
	private int textureSize = 512;

	private bool paused = false;
	YieldInstruction waitTimeFrame;
	private List<Texture2D> listAllocatedTextures = new List<Texture2D>();
	private List<GameObject> listAllocatedGOs = new List<GameObject>();
	private StringBuilder sbStatusLog = new StringBuilder();
	private StringBuilder sbAllocationLog = new StringBuilder();
	long bytesPerObject = 0;

	void Awake() {
		Application.lowMemory += Application_lowMemory;
		textStatus.text = "Initialized";
		textAllocationStatus.text = string.Empty;
		buttonStartMemTest.interactable = true;
		buttonPauseMemTest.interactable = false;
		textVersion.text = Application.unityVersion;
		if (Application.productName.Contains("64bit"))
			textVersion.text += "\n(64-bit)";

		textLastRunInfo.text = PlayerPrefs.GetString("lastruninfo");
		inputMemEstimate.text = "0";
		textAllocatedForGraphics.text = string.Format("Allocated memory for the graphics driver: {0:N0} bytes", UnityEngine.Profiling.Profiler.GetAllocatedMemoryForGraphicsDriver());
	}

	void OnDestroy() {
		Application.lowMemory -= Application_lowMemory;
	}

	private void Application_lowMemory() {
		textStatus.text = "<color=yellow>Low memory detected</color>";

		if (checkboxAutopauseOnLowMemory.isOn) {
			OnPauseMemTest();
			textStatus.text = "<color=yellow>Auto-paused test due to low memory detection</color>";
			textStatus.Rebuild(CanvasUpdate.PreRender);
		}
	}

	public void OnToggleAllocationType() {
		allocateTextureMem = checkboxAllocateTextures.isOn;
		headerInputSize.text = allocateTextureMem ? "ARGB32 square texture size:" : "GameObjects:";
	}

	public void OnStartOrStopMemTest() {
		if (null == waitTimeFrame)
			StartMemoryStress();
		else
			StopMemoryStress();
	}

	public void OnPauseMemTest() {
		// Unpause if paused
		paused = !paused;
		textStatus.text = paused ? "Paused" : string.Format("Continuing memory stress... (object size: {0:N0} MB)", bytesPerObject / (1024 * 1024));
		buttonPauseMemTest.GetComponentInChildren<Text>().text = paused ? "Unpause" : "Pause";

		if (!paused) {
			// Reset wait timer delay in case it was changed during the pause
			ReadDelayAllocationTime();
			waitTimeFrame = new WaitForSeconds(delayAllocationTime);
		}
	}

	void ReadDelayAllocationTime() {
		if (!float.TryParse(inputDelayTime.text, out delayAllocationTime) || delayAllocationTime < 0.0f) {
			delayAllocationTime = 0.1f;
			inputDelayTime.text = "0.1f";
		}

	}

	void ReadAllocationSize() {
		if (!int.TryParse(inputTextureSize.text, out textureSize) || textureSize <= 0 || textureSize > 8196) {
			textureSize = 512;
			inputTextureSize.text = "512";
		}
	}

	void StartMemoryStress() {
		buttonStartMemTest.GetComponentInChildren<Text>().text = "Stop Memory Stress";
		buttonPauseMemTest.interactable = true;
		paused = false;

		ReadDelayAllocationTime();
		ReadAllocationSize();

		if (allocateTextureMem) {
			var tmpTexture = AllocateTexture();
			bytesPerObject = UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(tmpTexture);
			Destroy(tmpTexture);
		} else {
			bytesPerObject = UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(new GameObject());
		}
		textStatus.text = string.Format("Beginning memory stress... (object size: {0:N0} MB)", bytesPerObject / (1024 * 1024));
		System.GC.Collect();

		waitTimeFrame = new WaitForSeconds(delayAllocationTime);

		StartCoroutine(AllocateMemoryOverTime());
	}

	Texture2D AllocateTexture() {
		return new Texture2D(textureSize, textureSize, TextureFormat.ARGB32, false);
	}

	void StopMemoryStress() {
		waitTimeFrame = null;
		paused = false;
		textStatus.text = "Ending memory stress";
		buttonStartMemTest.GetComponentInChildren<Text>().text = "Start Memory Stress";
		textAllocationStatus.text = string.Format("Flushing {0} allocations and calling GC", allocateTextureMem ? listAllocatedTextures.Count : listAllocatedGOs.Count);
		buttonPauseMemTest.GetComponentInChildren<Text>().text = "Pause";

		buttonStartMemTest.interactable = true;
		buttonPauseMemTest.interactable = false;
		foreach (var texture in listAllocatedTextures) {
			Destroy(texture);
		}
		foreach(var go in listAllocatedGOs) {
			Destroy(go);
		}
		
		listAllocatedTextures.Clear();
		listAllocatedGOs.Clear();
		System.GC.Collect();
	}

	const int BATCH_ALLOCATE = 100;
	IEnumerator AllocateMemoryOverTime() {
		// Use stringbuilder as much as possible to minimize the string creation/disposal per frame
		// We take 1 hit to pretty-print our number to be command separated (regionally)
		int batchCount = 0;

		while (null != waitTimeFrame) {
			if (delayAllocationTime <= 0.0f && textureSize < 1024) {
				// Allocate in batches of 100 textures the same frame, but only if texture size is < 1024
				if (--batchCount < 0)
					batchCount = BATCH_ALLOCATE;
				else
					yield return null;
			} else {
				yield return waitTimeFrame;
			}
			
			if (paused)
				continue;

			if (null == waitTimeFrame)
				break;

			sbStatusLog.Clear().Append("Last run generated: ");

			sbAllocationLog.Clear().Append("Current allocations: ");

			if (allocateTextureMem) {
				listAllocatedTextures.Add(AllocateTexture());
				sbAllocationLog.Append(listAllocatedTextures.Count).Append(" textures");
				sbStatusLog.Append(listAllocatedTextures.Count).Append(" textures at size ").Append(textureSize);
			} else {
				for (int i = 0; i < textureSize; i++)
					listAllocatedGOs.Add(new GameObject());

				sbAllocationLog.Append(listAllocatedGOs.Count).Append(" GameObjects");
				sbStatusLog.Append(listAllocatedGOs.Count).Append(" GameObjects");
			}
			long total = (bytesPerObject * listAllocatedTextures.Count) / (1024 * 1024);
			string totalStr = string.Format("{0:N0}", total);
			inputMemEstimate.text = totalStr;

			sbStatusLog.Append(" (total allocated = ").Append(totalStr).Append(" MB)");
			textAllocationStatus.text = sbAllocationLog.ToString();
			PlayerPrefs.SetString("lastruninfo", sbStatusLog.ToString());
			PlayerPrefs.Save();


		}
	}
}
