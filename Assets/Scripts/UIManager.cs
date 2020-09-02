using UnityEngine;
using DG.Tweening;
using UnityEngine.UI;
using System;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

[Serializable]
public class DataScores : IEquatable<DataScores>, IComparable<DataScores>
{
    public float score;
    public string name;
    public string date;

    public int CompareTo(DataScores other)
    {
        if (other == null)
            return 1;
        else
            return other.score.CompareTo(score);
    }

    public bool Equals(DataScores other)
    {
        if (other == null) 
            return false;
        return Mathf.Approximately(score, other.score);
    }
}

public class UIManager : MonoBehaviour
{
    [Header("Canvas Group")]
    public CanvasGroup transitionImage;
    public CanvasGroup menuHolder;
    public CanvasGroup gameHolder;
    public CanvasGroup ratingHolder;
    public CanvasGroup infoHolder;
    public CanvasGroup enterName; 

    [Header("Manager")]
    public GameManager gameManager;

    [Header("Other")]
    public float scores;
    public SpriteRenderer backgroundImage;
    public GameEvents gameEvents = null;
    Sequence s;
    List<DataScores> dataScores;
    DataController dataController;

    [Header("UI Elements")]
    public RectTransform logo;
    public Button button_play;
    public Button button_info;
    public Button button_user;
    [Space]
    public TextMeshProUGUI ratingHolderHeading;
    [Space]
    public Button button_setSores;
    public Text textField_PlayerName;
    [Space]
    public Button button_pause;
    public Button button_sound;
    [Space]
    public TextMeshProUGUI scoreText;
    public TextMeshProUGUI roundText;
    [Space]
    public GameObject ratingTable;
    TextMeshProUGUI[] table_elements;

    [Header("UI Params")]
    public float durationShake;
    public Vector3 strengthShake1;
    public Vector3 strengthShake3;
    public Vector3 strengthShake2;
    public Vector3 strengthShakeLogo;

    public void OpenURL(string url) // Событие OnClick
    {
        if(!String.IsNullOrEmpty(url))
        {
            Application.OpenURL(url);
        }
        else
        {
            Debug.LogWarning("OpenURL. URL is null or empty");
        }
    }

    float easeInOutQuad(float x)
    {
        // Исп. два графика функций. В точке 0.5 осуществляем переход
        return x < 0.5f ? 2f * x * x : 1f - Mathf.Pow(-2f * x + 2f, 2f) / 2f;
    }

    void CreateBackground()
    {
        Texture2D texture2D = new Texture2D(Screen.width, Screen.height);

        Color[] pix = new Color[Screen.width * Screen.height];
        Color[] templateColor = new Color[Screen.height];

        Color a = new Color(94f / 255f, 145f / 255f, 198f / 255f, 1.0f);
        Color b = new Color(152 / 255f, 103f / 255f, 212f / 255f, 1.0f);

        int i, j;

        // Инициализация 
        for (i = 0; i < Screen.height; i++)
        {
            templateColor[i] = Color.Lerp(a, b, easeInOutQuad(1f * i / Screen.height));
        }
       
        for (i = 0; i < Screen.width; i++)
        {
            for (j = 0; j < Screen.height; j++)
            {
                pix[j * texture2D.width + i] = templateColor[j];
            }
        }

        texture2D.SetPixels(pix);
        texture2D.Apply();
   
        backgroundImage.sprite = Sprite.Create(texture2D, new Rect(0, 0, Screen.width, Screen.height), Vector2.zero);
        RectTransform rectTransform = backgroundImage.GetComponent<RectTransform>();
        rectTransform.localScale = new Vector3(rectTransform.rect.width / Screen.width * 100f, rectTransform.rect.height / Screen.height * 100f);
    }

    void ShowHolder(Action hideHolder, Action showHolder)
    {
        if (showHolder == null || hideHolder == null)
            return;

        hideHolder();
        transitionImage.DOFade(1f, 0.1f).SetLoops(2, LoopType.Yoyo).OnComplete(
        () => {
            showHolder();
        });
    }

    IEnumerator CalculateScore(float score)
    {
        float scoreValue = (scores * scores) * score;

        while (scoreValue > 0)
        {
            scoreValue -= scores;
            gameEvents.score += scores;
            scoreText.text = "Score\n" + gameEvents.score.ToString();
            yield return null;
        }
    }

    void ScoreUpdate(float scores)
    {
        if (scores == 0)
        {
            gameEvents.score = 0;
            scoreText.text = "Score\n" + 0.ToString();
        }
        else
        {
            StartCoroutine(CalculateScore(scores));
        }
    }

    void RefreshUIElements()
    {
        roundText.text = "Round\n" + gameEvents.round.ToString();
        scoreText.text = "Score\n" + gameEvents.score.ToString();
    }

    private void OnEnable()
    {
        gameEvents.RefreshUIElements += RefreshUIElements;
        gameEvents.ScoreUpdated += ScoreUpdate;
        gameEvents.SetHighSores += SetHighSores;
    }

    private void OnDisable()
    {
        gameEvents.RefreshUIElements -= RefreshUIElements;
        gameEvents.ScoreUpdated -= ScoreUpdate;
        gameEvents.SetHighSores -= SetHighSores;
    }

    Color RandomColor()
    {
        return new Color(UnityEngine.Random.Range(0f, 1f), UnityEngine.Random.Range(0f, 1f), UnityEngine.Random.Range(0f, 1f), 1);
    }

    void ButtonInitialize()
    {
        button_setSores.onClick.AddListener(AddNewPlayer);

        button_play.onClick.AddListener(() => ShowHolder(
           () => {
               s.Pause();
               menuHolder.blocksRaycasts = false;
               menuHolder.alpha = 0f;
           },
           () => {
               gameHolder.blocksRaycasts = true;
               gameManager.enabled = true;
               gameHolder.alpha = 1f;
               backgroundImage.sortingOrder = -4;
           }
       ));

        button_pause.onClick.AddListener(() => ShowHolder(
            () => {
                s.Play();
                gameHolder.blocksRaycasts = false;
                gameManager.enabled = false;
                gameHolder.alpha = 0f;
                backgroundImage.sortingOrder = 0;
            },
            () => {
                menuHolder.blocksRaycasts = true;
                menuHolder.alpha = 1f;
            }
        ));

        button_info.onClick.AddListener(() => ShowHolder(
            () => {
                s.Pause();
                menuHolder.alpha = 0;
                menuHolder.blocksRaycasts = false;
            },
            () => {
                infoHolder.alpha = 1;
                infoHolder.blocksRaycasts = true;
            }
        ));


        button_user.onClick.AddListener(() => ShowHolder(
            () => {
                s.Pause();
                menuHolder.alpha = 0;
                menuHolder.blocksRaycasts = false;
            },
            () => {
                ratingHolder.alpha = 1;
                ratingHolder.blocksRaycasts = true;
                ratingHolderHeading.text = "HIGH SCORES";
            }
        ));

        infoHolder.GetComponent<Button>().onClick.AddListener(() => ShowHolder(
            () => {
                s.Play();
                infoHolder.alpha = 0;
                infoHolder.blocksRaycasts = false;
            },
            () => {
                menuHolder.alpha = 1;
                menuHolder.blocksRaycasts = true;
            }
        ));

        ratingHolder.GetComponent<Button>().onClick.AddListener(() => ShowHolder(
            () => {
                s.Play();
                ratingHolder.alpha = 0;
                ratingHolder.blocksRaycasts = false;
            },
            () => {
                menuHolder.alpha = 1;
                menuHolder.blocksRaycasts = true;
            }
        ));
    }

    void SetHighSores()
    {
        if(dataScores.Count == 0)
        {
            float current;
            
            // В таблице три столбца 
            for (int i = 0; i < table_elements.Length - 3; i += 3)
            {
                if (!float.TryParse(table_elements[i + 1].text, out current))
                    break;

                dataScores.Add(new DataScores() {score = current, name = table_elements[i].text, date = table_elements[i + 2].text });
            }
        }

        // Отобразить страницу с рейтингом
        ShowHolder(
            () =>
            {
                gameHolder.blocksRaycasts = false;
                gameManager.enabled = false;
                gameHolder.alpha = 0f;              
                backgroundImage.sortingOrder = -1;
            },
            () =>
            {
                ratingHolder.alpha = 1f;
                ratingHolderHeading.text = "GAME OVER";
            }
        );

        // Если попали в ТОП
        if (dataScores.Count < table_elements.Length || dataScores.Min().score < gameEvents.score)
        {         
            enterName.alpha = 1;
            enterName.blocksRaycasts = true;
        }
        else
        {
            ratingHolder.blocksRaycasts = true;
            gameEvents.score = 0;
            RefreshUIElements();
        }
    }

    void AddNewPlayer() // Событие кнопки 
    {
        // Если строка пустая, то кнопка ОК не сработает
        if (String.IsNullOrWhiteSpace(textField_PlayerName.text))
        {
            return;
        }

        ratingHolder.blocksRaycasts = true;
        enterName.alpha = 0;
        enterName.blocksRaycasts = false;
       
        dataScores.Add(new DataScores { score = gameEvents.score, name = textField_PlayerName.text, date = DateTime.Now.ToString("MMM d',' yyyy", 
            System.Globalization.CultureInfo.InvariantCulture) });
        dataScores.Sort();

        UpdateTable();

        gameEvents.score = 0;
        RefreshUIElements();
    }

    void UpdateTable()
    {
        if(table_elements == null || dataScores == null)
        {
            return;
        }

        int i = 0;

        // Обновить таблицу
        foreach (var item in dataScores)
        {
            table_elements[i].text = item.name;
            table_elements[i + 1].text = item.score.ToString();
            table_elements[i + 2].text = item.date;
            i += 3;
        }
    }

    void InitializeData()
    {
        table_elements = ratingTable.GetComponentsInChildren<TextMeshProUGUI>();
        dataController = new DataController(gameEvents);

        PlayerDataSerialize dataSerialize = dataController.LoadData<PlayerDataSerialize>(FileType.progress);
        if (dataSerialize != null && dataSerialize.dataScores != null && dataSerialize.dataScores.Length > 0)
        {
            dataScores = dataSerialize.dataScores.ToList();
            UpdateTable();
        }
        else
        {
            dataScores = new List<DataScores>();
        }

        dataController.LoadPlayerProgress();
        RefreshUIElements();
    }

#if UNITY_EDITOR

    private void OnApplicationQuit()
    {
        if (dataScores == null || dataScores.Count == 0)
        {
            return;
        }

        PlayerDataSerialize dataSerialize = new PlayerDataSerialize();
        dataSerialize.dataScores = dataScores.ToArray();

        dataController.SaveData<PlayerDataSerialize>(dataSerialize, FileType.progress);
        dataController.SavePlayerProgress();
    }

#elif UNITY_ANDROID

    private void OnApplicationPause()
    {
        if (dataScores == null || dataScores.Count == 0)
        {
            return;
        }

        PlayerDataSerialize dataSerialize = new PlayerDataSerialize();
        dataSerialize.dataScores = dataScores.ToArray();

        dataController.SaveData<PlayerDataSerialize>(dataSerialize, FileType.progress);
        dataController.SavePlayerProgress();
    }

#endif

    private void Start()
    {
        CreateBackground();
        InitializeData();

        scores = Mathf.Sqrt(scores);
        
        s = DOTween.Sequence();
        s.Insert(0f, logo.transform.DOShakePosition(durationShake, strengthShakeLogo, 0));
        s.Insert(1f, button_play.transform.DOShakePosition(durationShake, strengthShake1, 0));
        s.Insert(2f, button_info.transform.DOShakePosition(durationShake, strengthShake2, 0));
        s.Insert(3f, button_user.transform.DOShakePosition(durationShake, strengthShake3, 0));
        s.SetLoops(-1, LoopType.Yoyo);

        ButtonInitialize();
    }
}
