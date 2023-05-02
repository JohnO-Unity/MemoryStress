using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MainManager : MonoBehaviour {
	public Text textStatus;
	public Text textAllocationStatus;
	public Button buttonStartMemTest;
	public Button buttonPauseMemTest;
	public InputField inputDelayTime;
	public InputField inputTextureSize;
	public Text headerInputSize;
	public Toggle checkboxAllocateTextures;

	private bool allocateTextureMem = true;
	private float delayAllocationTime = 0.1f;
	private int textureSize = 512;

	private bool paused = false;
	YieldInstruction waitTimeFrame;
	private List<Texture2D> listAllocatedTextures = new List<Texture2D>();
	private List<GameObject> listAllocatedGOs = new List<GameObject>();

	void Awake() {
		Application.lowMemory += Application_lowMemory;
		textStatus.text = "Initialized";
		textAllocationStatus.text = string.Empty;
		buttonStartMemTest.interactable = true;
		buttonPauseMemTest.interactable = false;
	}

	void OnDestroy() {
		Application.lowMemory -= Application_lowMemory;
	}

	private void Application_lowMemory() {
		textStatus.text = "<color=yellow>Low memory detected</color>";
	}

	public void OnToggleAllocationType() {
		allocateTextureMem = checkboxAllocateTextures.isOn;
		headerInputSize.text = allocateTextureMem ? "Texture size:" : "GameObjects:";
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
		textStatus.text = paused ? "Paused" : "Continuing memory stress...";
		buttonPauseMemTest.GetComponentInChildren<Text>().text = paused ? "Unpause" : "Pause";
	}

	void StartMemoryStress() {
		textStatus.text = "Beginning memory stress...";
		buttonStartMemTest.GetComponentInChildren<Text>().text = "Stop Memory Stress";
		buttonPauseMemTest.interactable = true;
		paused = false;

		if (!float.TryParse(inputDelayTime.text, out delayAllocationTime) || delayAllocationTime < 0.0f) {
			delayAllocationTime = 0.1f;
			inputDelayTime.text = "0.1f";
		}
		if (!int.TryParse(inputTextureSize.text, out textureSize) || textureSize <= 0 || textureSize > 8196) {
			textureSize = 512;
			inputTextureSize.text = "512";
		}

		waitTimeFrame = new WaitForSeconds(delayAllocationTime);

		StartCoroutine(AllocateMemoryOverTime());
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

	IEnumerator AllocateMemoryOverTime() {
		while (null != waitTimeFrame) {
			yield return waitTimeFrame;
			
			if (paused)
				continue;

			if (null == waitTimeFrame)
				break;

			if (allocateTextureMem) {
				listAllocatedTextures.Add(new Texture2D(textureSize, textureSize));
				textAllocationStatus.text = string.Format("Current allocations: {0} textures", listAllocatedTextures.Count);
			} else {
				for (int i = 0; i < textureSize; i++)
					listAllocatedGOs.Add(new GameObject());

				textAllocationStatus.text = string.Format("Current allocations: {0} GameObjects", listAllocatedGOs.Count);
			}
		}
	}
}
