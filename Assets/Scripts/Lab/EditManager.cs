using System;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using Photon.Pun;

public class EditManager : MonoBehaviourPunCallbacks
{
    public Transform heldObject;

    [Header("Panels")]
    public GameObject lightPanel;
    public GameObject lensePanel;
    public GameObject prismPanel;
    public GameObject mirrorPanel;
    public GameObject customPanel;

    [Header("Menu")]
    public GameObject pauseMenu;
    public GameObject helpMenu;

    [Header("Save system")]
    public Animator saveAnimator;
    public Camera saveCamera;
    public GameObject lightPrefab;
    public GameObject concaveLensPrefab;
    public GameObject convexLensePrefab;
    public GameObject prismPrefab;
    public GameObject mirrorPrefab;

    //Pan camera
    protected Vector3 originPos;

    //Zoom
    [Header("Zoom settings")]
    public float zoomSpeed = 10f;
    public float minZoom = 5f;
    public float maxZoom = 20f;

    //Object transform manipulation
    public bool isDragging = false;
    public bool isRotating = false;
    public Vector3 dragOffset;

    //Scaling objects
    public bool isScaling = false;
    public float initialDistance;
    public Vector3 initialScale;

    public bool isSavedAlready;
    public string savedDate;
    public bool is3d;

    public MonoBehaviour[] disableScripts;

    private void Awake()
    {
        LoadData();
    }

    private void Update()
    {
        Vector3 mousePosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mousePosition.z = 0;

        if (Input.GetKeyDown(KeyCode.Escape) && !pauseMenu.activeSelf)
            PauseMenu();
        else if (Input.GetKeyDown(KeyCode.Escape) && pauseMenu.activeSelf && !helpMenu.activeSelf)
            PauseMenu();
        else if (Input.GetKeyDown(KeyCode.Escape) && pauseMenu.activeSelf && helpMenu.activeSelf)
            Help();

        if (Input.GetKeyDown(KeyCode.Delete) && heldObject != null && heldObject.tag != "Untagged")
            Delete();

        if (Input.GetMouseButtonDown(0) && !pauseMenu.activeSelf)
        {

            // if (is3d)
            // {
                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                RaycastHit hit;
                if (Physics.Raycast(ray, out hit))
                {
                    if (!EventSystem.current.IsPointerOverGameObject())
                    {
                        heldObject = hit.transform.root;
                        if(!is3d){
                            isDragging = true;
                            dragOffset = heldObject.position - mousePosition;
                        }
                        HandleObjectClick(hit.transform.root.tag);
                    } 
                } else {
                        if(!is3d && !EventSystem.current.IsPointerOverGameObject())
                            CloseAll();
                }
            //}
            // else
            // {
            //     RaycastHit2D hit = Physics2D.Raycast(mousePosition, Vector2.zero);
            //     if (hit.collider != null && !EventSystem.current.IsPointerOverGameObject())
            //     {
            //         heldObject = hit.transform.root;
            //         isDragging = true;
            //         dragOffset = heldObject.position - mousePosition;

            //         HandleObjectClick(hit.transform.root.tag);
            //     }
            //     else if (hit.collider == null && !EventSystem.current.IsPointerOverGameObject())
            //         CloseAll();
            // }
        }

        if (!pauseMenu.activeSelf)
        {
            if (Input.GetMouseButton(0) && isDragging && heldObject != null)
            {
                if (Input.GetKey(KeyCode.R))
                {
                    RotateObjectTowardsMouse(heldObject, mousePosition);
                    isRotating = true;
                }
                else if (Input.GetKey(KeyCode.S) && !heldObject.CompareTag("light"))
                {
                    if (!isScaling)
                    {
                        initialDistance = Vector2.Distance(heldObject.position, mousePosition);
                        initialScale = heldObject.localScale;
                        isScaling = true;
                    }
                    else
                    {
                        ScaleObject(heldObject, mousePosition, initialDistance, initialScale);
                    }
                }
                else
                {
                    if (!isRotating && !isScaling)
                    {
                        heldObject.position = new Vector2(mousePosition.x, mousePosition.y) + new Vector2(dragOffset.x, dragOffset.y);
                    }
                }
            }

            if (Input.GetMouseButtonUp(0))
            {
                isDragging = false;
                isRotating = false;
                isScaling = false;
            }

            //Pan camera movment system
            if (Input.GetMouseButtonDown(2)) // Right mouse button clicked
            {
                originPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            }
            else if (Input.GetMouseButton(2)) // Right mouse button held down
            {
                Vector3 difference = originPos - Camera.main.ScreenToWorldPoint(Input.mousePosition);
                Camera.main.transform.position += new Vector3(difference.x, difference.y, 0);
            }

            //Zoom camera system
            if (Camera.main.orthographic)
            {
                // For orthographic camera
                Camera.main.orthographicSize = Mathf.Clamp(Camera.main.orthographicSize - Input.GetAxis("Mouse ScrollWheel") * zoomSpeed, minZoom, maxZoom);
            }
        }

        if (Input.GetKeyDown(KeyCode.Escape) && helpMenu.activeSelf)
            Help();
    }

    public void PauseMenu()
    {
        pauseMenu.SetActive(!pauseMenu.activeSelf);

        Resources.UnloadUnusedAssets();

        foreach (MonoBehaviour script in disableScripts)
        {
            script.enabled = !pauseMenu.activeSelf;
        }

        if (is3d)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = pauseMenu.activeSelf;
        }
    }

    public void Save()
    {
        SaveData saveData = new SaveData();
        var objects = FindObjectsOfType<GameObject>();

        foreach (var obj in objects)
        {
            if (obj.tag == "light")
            {
                LightPrism lightPrism = obj.GetComponent<LightPrism>();

                LightData lightData = new LightData
                {
                    type = "light",
                    posX = lightPrism.transform.position.x,
                    posY = lightPrism.transform.position.y,
                    posZ = lightPrism.transform.position.z,
                    rotX = lightPrism.transform.rotation.eulerAngles.x,
                    rotY = lightPrism.transform.rotation.eulerAngles.y,
                    rotZ = lightPrism.transform.rotation.eulerAngles.z,
                    width = lightPrism.width,
                    waveLength = lightPrism.waveLength,
                    opacity = lightPrism.opacity,
                    whiteLight = lightPrism.whiteLight
                };

                saveData.lights.Add(lightData);
            }
        }

        foreach (var obj in objects)
        {
            ConcaveShape concaveShape = obj.GetComponent<ConcaveShape>();
            ConvexShape convexShape = obj.GetComponent<ConvexShape>();
            Glass glass = obj.GetComponent<Glass>();

            if (concaveShape != null || convexShape != null)
            {
                LenseData lenseData = new LenseData
                {
                    type = concaveShape != null ? "ConcaveShape" : "ConvexShape",
                    posX = (concaveShape != null ? concaveShape.transform.position : convexShape.transform.position).x,
                    posY = (concaveShape != null ? concaveShape.transform.position : convexShape.transform.position).y,
                    posZ = (concaveShape != null ? concaveShape.transform.position : convexShape.transform.position).z,
                    rotX = (concaveShape != null ? concaveShape.transform.rotation.eulerAngles : convexShape.transform.rotation.eulerAngles).x,
                    rotY = (concaveShape != null ? concaveShape.transform.rotation.eulerAngles : convexShape.transform.rotation.eulerAngles).y,
                    rotZ = (concaveShape != null ? concaveShape.transform.rotation.eulerAngles : convexShape.transform.rotation.eulerAngles).z,
                    scale = (concaveShape != null ? concaveShape.transform.localScale : convexShape.transform.localScale).x,
                    leftRadius = concaveShape != null ? concaveShape.leftRadius : convexShape.leftRadius,
                    rightRadius = concaveShape != null ? concaveShape.rightRadius : convexShape.rightRadius,
                    squareX = concaveShape != null ? concaveShape.squareX : convexShape.squareX,
                    squareY = concaveShape != null ? concaveShape.squareY : convexShape.squareY,
                    refractiveIndex = glass != null ? glass.refractiveIndex : 0,
                    transmission = glass != null ? glass.transmission : 0
                };

                saveData.lenses.Add(lenseData);
            }
        }

        foreach (var obj in objects)
        {
            if (obj.GetComponent<ConcaveShape>() == null && obj.GetComponent<ConvexShape>() == null && obj.GetComponent<Glass>() != null)
            {
                Glass glass = obj.GetComponent<Glass>();
                PrismData prismData = new PrismData();

                prismData.type = "Prism";
                prismData.posX = glass.transform.position.x;
                prismData.posY = glass.transform.position.y;
                prismData.posZ = glass.transform.position.z;
                prismData.rotX = glass.transform.rotation.eulerAngles.x;
                prismData.rotY = glass.transform.rotation.eulerAngles.y;
                prismData.rotZ = glass.transform.rotation.eulerAngles.z;
                prismData.scale = glass.transform.localScale.x;
                prismData.isLense3D = glass.isLense3D;
                prismData.typeOfLense3D = glass.typeOfLense3D;

                prismData.refractiveIndex = glass.refractiveIndex;
                prismData.transmission = glass.transmission;

                saveData.prisms.Add(prismData);
            }
        }

        foreach (var obj in FindObjectsOfType<GameObject>())
        {
            if (obj.tag == "reflect")
            {
                MirrorData mirrorData = new MirrorData();

                mirrorData.type = "Mirror";
                mirrorData.posX = obj.transform.position.x;
                mirrorData.posY = obj.transform.position.y;
                mirrorData.posZ = obj.transform.position.z;
                mirrorData.rotX = obj.transform.rotation.eulerAngles.x;
                mirrorData.rotY = obj.transform.rotation.eulerAngles.y;
                mirrorData.rotZ = obj.transform.rotation.eulerAngles.z;
                mirrorData.scaleX = obj.transform.localScale.x;
                mirrorData.scaleY = obj.transform.localScale.y;
                mirrorData.scaleZ = obj.transform.localScale.z;

                saveData.mirrors.Add(mirrorData);
            }
        }

        saveData.camX = Camera.main.transform.position.x;
        saveData.camY = Camera.main.transform.position.y;
        saveData.camZ = Camera.main.transform.position.z;

        saveData.rotX = Camera.main.transform.rotation.x;
        saveData.rotY = Camera.main.transform.rotation.y;
        saveData.rotZ = Camera.main.transform.rotation.z;

        //Create save folder
        string timestamp = CrossData.isSave ? CrossData.date : isSavedAlready ? savedDate : DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        string directoryPath = CrossData.isSave ? $"{Application.dataPath}/saves/{CrossData.mode}/{CrossData.date}" : isSavedAlready ? $"{Application.dataPath}/saves/{CrossData.mode}/{savedDate}" : $"{Application.dataPath}/saves/{CrossData.mode}/{timestamp}";
        Directory.CreateDirectory(directoryPath);

        //Save data
        string json = JsonUtility.ToJson(saveData, true);
        File.WriteAllText($"{Application.dataPath}\\saves\\{CrossData.mode}\\{timestamp}\\{timestamp}_Spectra.json", json);
        CaptureScreenshot($"{Application.dataPath}\\saves\\{CrossData.mode}\\{timestamp}\\{timestamp}_Spectra.png");

        //Save animation trigger
        saveAnimator.SetTrigger("Saved");

        savedDate = isSavedAlready ? savedDate : timestamp;
        isSavedAlready = true;
    }

    public void LoadData()
    {
        if (CrossData.isSave)
        {
            SaveData saveData = CrossData.saveData;

            Camera.main.transform.position = new Vector3(saveData.camX, saveData.camY, saveData.camZ);

            foreach (LightData lightData in saveData.lights)
            {
                GameObject spawnedLight = Instantiate(lightPrefab);
                LightPrism lightPrism = spawnedLight.GetComponent<LightPrism>();

                spawnedLight.transform.position = new Vector3(lightData.posX, lightData.posY, lightData.posZ);
                spawnedLight.transform.rotation = Quaternion.Euler(lightData.rotX, lightData.rotY, lightData.rotZ);

                lightPrism.width = lightData.width;
                lightPrism.waveLength = lightData.waveLength;
                lightPrism.opacity = lightData.opacity;
                lightPrism.whiteLight = lightData.whiteLight;
            }

            foreach (LenseData lenseData in saveData.lenses)
            {
                GameObject spawnedLense = lenseData.type == "ConcaveShape" ? Instantiate(concaveLensPrefab) : Instantiate(convexLensePrefab);
                Lense lense = spawnedLense.GetComponent<Lense>();
                Glass glass = spawnedLense.GetComponent<Glass>();

                spawnedLense.transform.position = new Vector3(lenseData.posX, lenseData.posY, lenseData.posZ);
                spawnedLense.transform.rotation = Quaternion.Euler(lenseData.rotX, lenseData.rotY, lenseData.rotZ);
                spawnedLense.transform.localScale = new Vector3(lenseData.scale, lenseData.scale, lenseData.scale);

                lense.leftRadius = lenseData.leftRadius;
                lense.rightRadius = lenseData.rightRadius;

                lense.squareX = lenseData.squareX;
                lense.squareY = lenseData.squareY;

                glass.refractiveIndex = lenseData.refractiveIndex;
                glass.transmission = lenseData.transmission;
            }

            foreach (PrismData prismData in saveData.prisms)
            {
                Debug.Log("Instantiating: " + (prismData.isLense3D ? prismData.typeOfLense3D == "concave" ? "concaveLensPrefab" : "convexLensPrefab" : "prismPrefab"));

                GameObject spawnedPrism = Instantiate(prismData.isLense3D ? prismData.typeOfLense3D == "concave" ? concaveLensPrefab : convexLensePrefab : prismPrefab);
                Glass glass = spawnedPrism.GetComponent<Glass>();

                spawnedPrism.transform.position = new Vector3(prismData.posX, prismData.posY, prismData.posZ);
                spawnedPrism.transform.rotation = Quaternion.Euler(prismData.rotX, prismData.rotZ, prismData.rotZ);
                spawnedPrism.transform.localScale = new Vector3(prismData.scale, prismData.scale, prismData.scale);

                glass.refractiveIndex = prismData.refractiveIndex;
                glass.transmission = prismData.transmission;
            }

            foreach (MirrorData mirrorData in saveData.mirrors)
            {
                GameObject spawnedMirror = Instantiate(mirrorPrefab);

                spawnedMirror.transform.position = new Vector3(mirrorData.posX, mirrorData.posY, mirrorData.posZ);
                spawnedMirror.transform.rotation = Quaternion.Euler(mirrorData.rotX, mirrorData.rotY, mirrorData.rotZ);
                spawnedMirror.transform.localScale = new Vector3(mirrorData.scaleX, mirrorData.scaleY, mirrorData.scaleZ);
            }
        }
    }

    public void CaptureScreenshot(string filePath)
    {
        saveCamera.gameObject.SetActive(true);
        saveCamera.transform.position = Camera.main.transform.position;
        saveCamera.transform.rotation = Camera.main.transform.rotation;

        RenderTexture rt = new RenderTexture(Screen.width, Screen.height, 24);
        saveCamera.targetTexture = rt;
        Texture2D screenShot = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
        saveCamera.Render();
        RenderTexture.active = rt;
        screenShot.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
        screenShot.Apply();

        saveCamera.targetTexture = null;
        RenderTexture.active = null;
        Destroy(rt);

        byte[] bytes = screenShot.EncodeToPNG();
        File.WriteAllBytes(filePath, bytes);
        saveCamera.gameObject.SetActive(false);
    }

    public void Home()
    {
        SceneManager.LoadScene(0);
    }

    public void Help()
    {
        helpMenu.SetActive(!helpMenu.activeSelf);
    }

    public void Quit()
    {
        Application.Quit();
    }

    public void RotateObjectTowardsMouse(Transform obj, Vector3 mousePos)
    {
        Vector2 direction = (mousePos - obj.position).normalized;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        obj.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
    }

    public void ScaleObject(Transform obj, Vector3 mousePos, float initialDist, Vector3 initialScal)
    {
        float currentDistance = Vector2.Distance(obj.position, mousePos);
        float scaleFactor = currentDistance / initialDist;
        obj.localScale = initialScal * scaleFactor;
    }

    public void HandleObjectClick(string tag)
    {
        Vector3 cameraPosition = Camera.main.transform.position;
        Vector3 cameraDirection = Camera.main.transform.forward;
        Vector3 rightDirection = Camera.main.transform.right;

        // Określ, jak daleko chcesz przesunąć spawnPosition w prawo
        float offset = 4f; // Możesz zmieniać tę wartość w zależności od potrzeb
        Vector3 spawnPosition = cameraPosition + cameraDirection * 5 + rightDirection * offset;

        GameObject panel = null;

        switch (tag)
        {
            case "light":
                panel = lightPanel;
                break;
            case "refract":
                if (heldObject.name == "CO")
                    panel = customPanel;
                else if (heldObject.GetComponent<Glass>().isLense3D)
                    panel = lensePanel;
                else if (heldObject.GetComponent<Lense>() == null)
                    panel = prismPanel;
                else
                    panel = lensePanel;
                break;
            case "reflect":
                panel = heldObject.name == "CO" ? customPanel : mirrorPanel;
                break;
        }

        if (panel != null)
        {
            //CloseAll(); 
            Panel panelComponent = panel.GetComponent<Panel>();
            if (panelComponent != null)
            {
                panelComponent.LoadData(heldObject);
                if (is3d){
                    panel.transform.parent.gameObject.transform.position = spawnPosition;
                    Vector3 directionToCamera = Camera.main.transform.position - panel.transform.position;
                    panel.transform.parent.gameObject.transform.rotation = Quaternion.LookRotation(-directionToCamera);
                }else
                    CloseAll();
                panel.SetActive(true);
            }
        }
    }


    private void CloseAll()
    {
        lightPanel.SetActive(false);
        lensePanel.SetActive(false);
        prismPanel.SetActive(false);
        mirrorPanel.SetActive(false);
        if(customPanel)
            customPanel.SetActive(false);
    }

    public void Delete()
    {
        CloseAll();
        Destroy(heldObject.gameObject);
        Resources.UnloadUnusedAssets();
    }
}
