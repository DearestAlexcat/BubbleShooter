using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using System.Linq;
using UnityEngine.EventSystems;
using System;

[Serializable]
public class GameManagerParameters
{
    [Header("Other")]
    public int numberAttachedBalls;
    public Material trailRendererMaterial;

    [Header("Field params")]
    public int quantity_x;
    public int quantity_y;

    [Header("Lead ball params")]
    public float speedLeadBall;
    public float sizeNextBall;

    [Header("Small ball params")]
    public float speedSmallBall;
    public Vector3 sizeAndShiftSmallBall;

    [Header("Wave params")]
    public int waveLength;
    public float sizeShiftWave;
    public float durationWave;
    public Ease ease;

    [Header("Destroy balls params")]
    public float destroyScale;
    public float destroySpeed;
    public float fallingBallSpeed;
    public float jumpHeight;

    [Header("Add row balls params")]
    public float durationAddRow;

    [Header("Initialize balls")]
    public float durationInitBalls;
}

public class GameManager : MonoBehaviour
{
    #region Variables

    enum GameState
    {
        None,
        NewGame,
        Aiming,
        BubbleFlight
    }

    public BallController prefabBalls;
    public RectTransform Arrow = null; // Стартовая позиция для следующего мяча
    public RectTransform bubbleHolder;

    public GameEvents gameEvents = null;

    GameState gameState = GameState.None;

    BallController nextBall = null;
    BallController leadBall = null;

    Vector3 ballDirection;
    Vector3 startPosition;
    Vector3 endPosition;

    float angleZ;
    float ballSize;
    float prevAngleZ = -1;
    int quantity_x;

    int numberMissedChains;

    Sprite[] bubbleSprites;
    SimpleObjectPool objectsPool = null;
    
    SortedDictionary<int, BallController> balls = new SortedDictionary<int, BallController>();
    LinkedList<BallController> ballsTrajectory = new LinkedList<BallController>();

    [SerializeField]
    GameManagerParameters gmp = new GameManagerParameters();
    DataController dataController = null;


    #endregion

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;

        Vector2 inDirection = Arrow.position;
        Vector2 inNormal = Arrow.up;

        RaycastHit2D raycastHit = Physics2D.Raycast(inDirection, inNormal);
        Vector3 reflectVect = Vector3.Reflect(inNormal, raycastHit.normal);

        while (raycastHit.collider != null)
        {
            Gizmos.DrawLine(inDirection, raycastHit.point);
            Gizmos.DrawRay(raycastHit.point, reflectVect * 100f);

            if (raycastHit.transform.CompareTag("Ball"))
            {
                //Debug.Log(raycastHit.collider.name);
                //target = raycastHit.collider.gameObject.GetComponent<BallController>();
                break;
            }

            inDirection = raycastHit.point;
            inNormal = reflectVect;
            //inDirection + raycastHit.normal * 0.01f - cмещаем raycastHit.point, чтобы дважды не реагировать на тот же collider
            raycastHit = Physics2D.Raycast(inDirection + raycastHit.normal * 0.01f, inNormal);
            reflectVect = Vector3.Reflect(inNormal, raycastHit.normal);
        }
    }

    bool CheckCollision(BallController ball)
    {
        bool isDetected = false;

        // other ball
        foreach (var otherBall in balls)
        {
            if (IntersectRect(ball.rectTransform, otherBall.Value.rectTransform))
            {
                isDetected = true;
                break;
            }
        }

        // Или пересекли мяч на поле, или вышли за верхние границы, или ничего
        return isDetected || (ball.rectTransform.localPosition.y + ball.rectTransform.localScale.x) > 0f;
    }

    void CheckPosition(BallController ball, bool isAnime = true)
    {
        float half_size = ballSize * 0.5f - 0.001f;

        // left
        if (ball.rectTransform.localPosition.x < half_size)
        {
            float value = half_size - ball.rectTransform.localPosition.x;
            Vector3 correctPosition = ball.rectTransform.localPosition;

            correctPosition.x += value * 2f; // Проецируем вектор вправо

            // Заталкиваем на поле в нужную позицию
            ball.rectTransform.localPosition = correctPosition;
            ball.direction.x = -ball.direction.x;
        }

        // right
        if (ball.rectTransform.localPosition.x > bubbleHolder.rect.width - half_size)
        {
            float value = (ball.rectTransform.localPosition.x - (bubbleHolder.rect.width - half_size));
            Vector3 correctPosition = ball.rectTransform.localPosition;

            correctPosition.x -= value * 2f;  // Проецируем вектор влево

            // Заталкиваем на поле в нужную позицию
            ball.rectTransform.localPosition = correctPosition;
            ball.direction.x = -ball.direction.x;
        }

        // Если достигли конца траектории полета, возвращаем мяч на старт
        if (isAnime && ball.rectTransform.localPosition.y > endPosition.y)
        {
            ball.direction = ballDirection;
            ball.rectTransform.localPosition = startPosition;
        }
    }

    bool IntersectCircle(RectTransform b)
    {
        Vector2 d = leadBall.rectTransform.anchoredPosition - b.anchoredPosition;
        
        float r = ballSize * 0.5f + ballSize * 0.3f;
        //return d.x * d.x + d.y * d.y <= r * r;

        float sqrD = d.x * d.x + d.y * d.y;
        if (sqrD <= r * r)
        {
            // Устраняем пересечение, чтобы точнее просчитать позицию для leadBall в AttachBubble
            leadBall.rectTransform.localPosition -= leadBall.direction * (ballSize - Mathf.Sqrt(sqrD));
            return true;
        }

        return false;
    }

    bool IntersectRect(RectTransform a, RectTransform b)
    {
        if (Mathf.Abs(a.anchoredPosition.x - b.anchoredPosition.x) >
            (a.localScale.x / 2f * 100f + b.localScale.x / 2f * 100f))
        {
            return false;
        }

        if (Mathf.Abs(a.anchoredPosition.y - b.anchoredPosition.y) >
            (a.localScale.y / 2f * 100f + b.localScale.y / 2f * 100f))
        {
            return false;
        }

        return true;
    }

    void CheckCollision()
    {
        // top
        if (leadBall.rectTransform.localPosition.y + leadBall.rectTransform.localScale.x > 0f)
        {
            CollisionDetected();
            return;
        }

        // other ball
        foreach (var ball in balls)
        {
            if (IntersectCircle(ball.Value.rectTransform))
            {
                CollisionDetected();
                return;
            }
        }

        float half_size = ballSize * 0.5f - 0.001f;

        // left
        if (leadBall.rectTransform.localPosition.x < half_size)
        {
            leadBall.rectTransform.localPosition -= leadBall.direction * (half_size - leadBall.rectTransform.localPosition.x); // Заталкиваем на поле
            leadBall.direction.x = -leadBall.direction.x;
        }

        // right
        if (leadBall.rectTransform.localPosition.x > bubbleHolder.rect.width - half_size)
        {
            leadBall.rectTransform.localPosition -= leadBall.direction * (leadBall.rectTransform.localPosition.x - (bubbleHolder.rect.width - half_size)); // Заталкиваем на поле
            leadBall.direction.x = -leadBall.direction.x;
        }
    }
 
    void CheckEndGame()
    {
        if(balls.Count == 0) // Если уничтожили все мячи
        {
            quantity_x = gmp.quantity_x;
            gameEvents.round++;
            gameEvents.RefreshUIElements();
            StartCoroutine(AddRow(quantity_x));
        }
        else 
        {
            KeyValuePair<int, BallController> ballLast = balls.Last();
            quantity_x = balls.Count == 0 ? 0 : ballLast.Key / gmp.quantity_y + 1;  // обновляем кол-во строк
           
            if (ballLast.Value.rectTransform.localPosition.y - Arrow.localPosition.y <= ballSize) // Если достигли границы по y
            {
                gameState = GameState.NewGame;
                gameEvents.SetHighSores();
            }
        }
    }

    void AddListIndexers(List<int> indexers, int i, int j)
    {
        if (CheckBounds(i, j))
        {
            int v = i * gmp.quantity_y + j;
            if (!indexers.Contains(v) && balls.ContainsKey(v))
            {
                indexers.Add(v);
            }
        }
    }

    void WaveAnimation()
    {
        List<int> indexers = new List<int>(6);

        Sequence s = DOTween.Sequence();
        s.OnComplete(FinalizeOperations);

        Vector3 endValue, direction;
        float localOtstup = gmp.sizeShiftWave;

        int i, j, k, l, x, index;

        i = leadBall.id / gmp.quantity_y;
        j = leadBall.id % gmp.quantity_y;
        indexers.Add(i * gmp.quantity_y + j);

        int numberAdded = 0; // Кол-во элементов чьих соседей уже нашли
        int previousSize; // Кол-во элементов для которых уже установлена анимация

        for (k = 0; k < gmp.waveLength; k++)
        {
            previousSize = indexers.Count;

            for (x = indexers.Count - 1; x >= numberAdded; x--)
            {
                i = indexers[x] / gmp.quantity_y;
                j = indexers[x] % gmp.quantity_y;

                AddListIndexers(indexers, i - 1, j);
                AddListIndexers(indexers, i, j + 1);
                AddListIndexers(indexers, i + 1, j);
                AddListIndexers(indexers, i, j - 1);

                if (((i + 1) & 1) == 0)
                {
                    AddListIndexers(indexers, i - 1, j + 1);
                    AddListIndexers(indexers, i + 1, j + 1);
                }
                else
                {
                    AddListIndexers(indexers, i - 1, j - 1);
                    AddListIndexers(indexers, i + 1, j - 1);
                }
            }

            numberAdded = previousSize;

            // Анимируем последние добавленные элементы, исключая первый (leadBall)
            for (l = indexers.Count - 1; l >= previousSize; l--)
            {
                index = indexers[l];

                direction = balls[index].rectTransform.position - leadBall.rectTransform.position;
                direction.Normalize();
                balls[index].direction = direction;
                endValue = balls[index].rectTransform.localPosition + balls[index].direction * localOtstup;
                // Установить анимцию соседним пузырям
                s.Insert(0, balls[index].rectTransform.DOLocalMove(endValue, gmp.durationWave).SetEase(gmp.ease).SetLoops(2, LoopType.Yoyo));
            }

            // Уменьшаем отступ для элементов следующей волны
            localOtstup *= 0.5f;
        }

        // 1f + 1f / gmp.sizeShiftWave

        // Установить анимцию для leadBall
        s.Insert(0, leadBall.rectTransform.DOScale(ballSize / 100f + ballSize / 100f / gmp.sizeShiftWave, gmp.durationWave).SetEase(gmp.ease).SetLoops(2, LoopType.Yoyo));
    }

    void UpdateScore()
    {
        if(listSingleColor.Count > 0 || listOfIsolated.Count > 0)
        {
            gameEvents.ScoreUpdated(listSingleColor.Count + listOfIsolated.Count);
        }
    }

    void RemoveListSingleColor()
    {
        foreach (var item in listSingleColor)
        {
            balls[item].rectTransform.localScale = new Vector3(1f, 1f, 1f);
            balls[item].ballImage.color = Color.white; // Восстанавливаем прозрачность
            objectsPool.ReturnObject(balls[item]);
            balls.Remove(item);
        }

        listSingleColor.Clear();
    }

    void AttachBubble()
    {
        Vector3 correctPosition;

        float half = ballSize / 2f;

        // По координатам вычисляем индексы элемента 
        int i = (int)Mathf.Floor(-leadBall.rectTransform.localPosition.y / ballSize);
        int j = (int)Mathf.Floor(leadBall.rectTransform.localPosition.x / ballSize);

        if (i < 0) i = 0;

        if ((i & 1) != 0) // если попали на сдвинутую строку, сдвигаем пузырь влево, т.к. индексы ищем в квадратной матрице
        {
            j = (int)Mathf.Floor((leadBall.rectTransform.localPosition.x - ballSize * 0.5f) / ballSize);
        }

        //if (i >= quantity_x) i = quantity_x - 1;
        if (j < 0) j = 0;
        if (j >= gmp.quantity_y) j = gmp.quantity_y - 1;

        int v = i * gmp.quantity_y + j;

        // Если место свободно, присоединяем, иначе опускаемся ниже
        while (balls.ContainsKey(v))
        {
            i++;
            v = i * gmp.quantity_y + j;
        }

        // Установливаем пузырь в корректную позицию в гексагональной сетке
        correctPosition.x = j * ballSize + half;
        correctPosition.y = i * -ballSize - half;
        correctPosition.z = 0f;

        if ((i & 1) != 0) // если стали на сдвинутую строку
        {
            correctPosition.x += ballSize * 0.5f;
        }

        leadBall.id = v;
        balls.Add(v, leadBall);

        KeyValuePair<int, BallController> ballLast = balls.Last();
        quantity_x = balls.Count == 0 ? 0 : ballLast.Key / gmp.quantity_y + 1;  // обновляем кол-во строк

        leadBall.trailRenderer.enabled = false;
        leadBall.rectTransform.localPosition = correctPosition;

        AudioManager.Instance.PlaySound("Attach");
    }

    void CollisionDetected()
    {
        gameState = GameState.None;
        
        AttachBubble();

        int i = leadBall.id / gmp.quantity_y;
        int j = leadBall.id % gmp.quantity_y;

        DFS(i, j, leadBall.ballType); // найти все цепочки одноцветных
        //HighlightingIsolated(); // поиск изолированных

        WaveAnimation();
    }

    void SearchIsolated()
    {
        HighlightingIsolated(); // поиск изолированных
        UpdateScore();
        RemoveListSingleColor();

        if (listOfIsolated.Count > 0)
        {
            StartCoroutine(DOLocalJump());
        }
        else
        {
            InitializeBalls();
        }
    }

    IEnumerator DOLocalJump()
    {
        float gravity = 9.81f;

        // За каждым элементом закреплен свой вектор движения
        Vector3[] directions = new Vector3[listOfIsolated.Count];

        bool isJump = true;

        var listIndex = listOfIsolated.ToList();
        int item, i;

        Vector3 jumpDirection;
        jumpDirection.x = 0;
        jumpDirection.z = 0;

        // Инициализируем высоту прыжка для каждого элемента
        for (i = listIndex.Count - 1; i >= 0; i--)
        {
            item = listIndex[i];
            balls[item].ballImage.sortingOrder = -1; //*
            jumpDirection.y = Mathf.Sqrt(gmp.jumpHeight * UnityEngine.Random.Range(0.88f, 1.2f) * gravity);
            directions[i] = jumpDirection;
        }

        while (listOfIsolated.Count > 0)
        {
            for (i = listIndex.Count - 1; i >= 0; i--)
            {
                item = listIndex[i];

                if (!isJump) // Если не надо прыгать, падаем
                {
                    directions[i].y -= gravity * Time.deltaTime; // Со временем скорость падения увеличивается
                }

                if ((balls[item].rectTransform.localPosition.y + balls[item].rectTransform.localScale.x) > bubbleHolder.rect.y)
                {
                    balls[item].rectTransform.localPosition += directions[i] * Time.deltaTime * gmp.fallingBallSpeed * 100f;
                }
                else
                {
                    listOfIsolated.Remove(item);
                    balls[item].ballImage.sortingOrder = -2; // *
                    objectsPool.ReturnObject(balls[item]);
                    balls.Remove(item);

                    listIndex = listOfIsolated.ToList();
                }
            }

            /*foreach (var item in listOfIsolated.ToList()) {
            }*/

            isJump = false;
            yield return null;
        }

        InitializeBalls();
    }

    void AnimListSingleColor()
    {
        Sequence s = DOTween.Sequence();
        s.OnComplete(SearchIsolated);

        foreach (var item in listSingleColor)
        {
            s.Insert(0, balls[item].rectTransform.DOScale(ballSize / 100f + gmp.destroyScale, gmp.destroySpeed)); 
            s.Insert(0, balls[item].ballImage.DOFade(0f, gmp.destroySpeed));
        }
    }

    void FinalizeOperations() // StartDestroyAnimations
    {
        try
        {
            // Цепочка должна состоять из 3 и более элементов
            if (listSingleColor.Count > 2)
            {
                AnimListSingleColor();              
            }
            else
            {
                // Если цепочка не образовалась убираем маркеры
                foreach (var item in listSingleColor)
                {
                    balls[item].ballType = -balls[item].ballType;
                }

                listSingleColor.Clear();

                numberMissedChains++;

                if (numberMissedChains == gmp.numberAttachedBalls)
                {
                    numberMissedChains = 0;
                    quantity_x++; // Добавить строчку
                    StartCoroutine(AddRow(1, SearchIsolated));
                }
                else
                {
                    InitializeBalls();
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError(ex.Message);
        }
    }

    IEnumerator AddRow(int numberRows, Action OnComplete = null)
    {
        Sequence s = null;

        for (int i = 0; i < numberRows; i++)
        {
            s = DOTween.Sequence();

            // temp_balls временно хранит элементы с валидными ключами
            SortedDictionary<int, BallController> temp_balls = new SortedDictionary<int, BallController>();

            Vector3 shift;
            float half_size = ballSize / 2f;
            int row;

            InitializeField(1, gmp.quantity_y, -1); // -1 - добавляем строку сверху

            // Анимация спуска
            foreach (var item in balls)
            {
                item.Value.id += gmp.quantity_y;
                temp_balls.Add(item.Value.id, item.Value);

                row = item.Value.id / gmp.quantity_y;

                // При опускании пузырей позиция по x меняется
                shift.x = item.Value.rectTransform.localPosition.x;
                shift.x += ((row & 1) != 0) ? half_size : -half_size;

                shift.y = item.Value.rectTransform.localPosition.y - ballSize;
                shift.z = 0f;

                s.Insert(0, item.Value.rectTransform.DOLocalMove(shift, gmp.durationAddRow));
            }

            balls = temp_balls;

            yield return s.WaitForCompletion();
        }

        OnComplete?.Invoke();
    }

    #region Initialize field & pool

    void InitializeParams()
    {
        quantity_x = gmp.quantity_x;
        // + 0.5f корректировка размера шаров, чтобы помещались сдвинутые
        ballSize = bubbleHolder.rect.width / (gmp.quantity_y + 0.5f);
        bubbleSprites = Resources.LoadAll<Sprite>("bubble_sprites"); 
    }

    void InitializeField(int quantity_x, int quantity_y, int posX = 0, int posY = 0)
    {
        if (quantity_y != 0 && quantity_x != 0)
        {
            Vector2 position = new Vector2(); 
            Vector2 localScale = new Vector2(ballSize / 100f, ballSize / 100f);
           
            float half_size = ballSize / 2f;

            position.x = half_size * Mathf.Sign(posY);
            position.y = -half_size * Mathf.Sign(posX);

            int colorIndex = UnityEngine.Random.Range(0, bubbleSprites.Length);

            for (int i = posX; i < quantity_x + posX; i++)
            {
                if ((i & 1) != 0)
                {
                    position.x += half_size;
                }

                for (int j = posY; j < quantity_y + posY; j++)
                {
                    BallController ball = objectsPool.GetObject();

                    ball.id = i * quantity_y + j; // Важно знать кол-во столбцов, кол-во строк не важно

                    //colorIndex = UnityEngine.Random.Range(0, bubbleSprites.Length);

                    colorIndex++;
                    colorIndex %= bubbleSprites.Length;

                    ball.ballImage.sprite = bubbleSprites[colorIndex];
                    ball.ballType = colorIndex + 1; // Индексация начинается с 1

                    ball.rectTransform.localScale = localScale;

                    ball.rectTransform.localPosition = position;

                    position.x += ballSize;
                    balls.Add(ball.id, ball);
                }

                position.x = half_size; // Возвращаем в начало
                position.y -= ballSize;
            }
        }
    }

    void InitializeObjectsPool()
    {
        objectsPool = GetComponent<SimpleObjectPool>();
        objectsPool.prefab = prefabBalls;
        objectsPool.bubbleHolder = bubbleHolder;
        objectsPool.InitializePool(100);  // Предустановка
    }

    #endregion

    #region Initialize leadBall & nextBall (Animation)

    int FindNextColor()
    {
        if(balls.Count == 0)
        {
            return 1;
        }

        List<int> idx = new List<int>();

        int i, j, v, index;

        for (j = 0; j < gmp.quantity_y; j++)
        {
            for (i = quantity_x - 1; i >= 0; i--)
            {
                v = i * gmp.quantity_y + j;

                if (balls.ContainsKey(v))
                {
                    idx.Add(v);
                    break;
                }
            }
        }
        
        index = UnityEngine.Random.Range(0, idx.Count);

        return balls[idx[index]].ballType;
    }

    void InitializeBalls()
    {
        if (nextBall == null)
        {
            nextBall = GetBall();
        }

        leadBall = nextBall;
        nextBall = GetBall();

        nextBall.rectTransform.localScale = Vector2.zero;
        Vector3 pos = nextBall.rectTransform.localPosition;
        nextBall.rectTransform.localPosition = new Vector3(pos.x, pos.y - Arrow.sizeDelta.y * 0.5f, pos.z);

        Sequence s = DOTween.Sequence();
        s.OnComplete(() => {
            leadBall.trailRenderer.enabled = true;
            gmp.trailRendererMaterial.mainTexture = leadBall.ballImage.sprite.texture;
            nextBall.rectTransform.DOScale(ballSize / 100f * gmp.sizeNextBall, gmp.durationInitBalls).OnComplete(() => { gameState = GameState.Aiming; CheckEndGame(); });
        });

        s.Insert(0, leadBall.rectTransform.DOLocalMove(Arrow.localPosition, gmp.durationInitBalls));
        s.Insert(0, leadBall.rectTransform.DOScale(new Vector2(ballSize / 100f, ballSize / 100f), gmp.durationInitBalls));
    }

    BallController GetBall()
    {
        BallController ball = objectsPool.GetObject();
        int colorIndex = FindNextColor(); // UnityEngine.Random.Range(0, bubbleSprites.Length); Помогаем игроку проходить игру

        ball.ballImage.sprite = bubbleSprites[colorIndex - 1];  // -1 тк индексация массива начинается с 0
        ball.ballType = colorIndex;                             // Индексация ball.ballType начинается с 1

        // Инициализация размера пузыря (уменьшаем). 
        ball.rectTransform.localScale = new Vector2(ballSize / 100f * gmp.sizeNextBall, ballSize / 100f * gmp.sizeNextBall); 
        ball.rectTransform.localPosition = Arrow.localPosition;
        return ball;
    }

    #endregion

    #region Trajectory flight

    void SetTrajectoryFlight()
    {
        DeterminingDirection();
        MiniBallsTrajectoryСorrection();
    }

    void DeterminingDirection()
    {
        // Делаем преобразование координаты мыши из мирового пространства (через камеру пространства экрана - холст камеры), чтобы получить 2D-положение.
        Vector3 mousePosition = Input.mousePosition;
        mousePosition.z = 100.0f;   // расстояние от плоскости до камеры
        mousePosition = Camera.main.ScreenToWorldPoint(mousePosition);

        ballDirection = mousePosition - Arrow.position;
        ballDirection.Normalize();
        angleZ = Mathf.Atan2(ballDirection.y, ballDirection.x) * Mathf.Rad2Deg - 90f;

        // Ограничиваем угол поворота на 15 градусов
        float angleLimit = 90 - 15;

        // -180 ... -90 ... 0 ... 90 | -270 ... -180

        if (angleZ > angleLimit || angleZ < -180f)
        {
            angleZ = angleLimit;
        }
        else if (angleZ < -angleLimit && angleZ >= -180f)
        {
            angleZ = -angleLimit;
        }

        //Debug.Log("angleZ " + angleZ);
        //Arrow.localRotation = Quaternion.Euler(0f, 0f, angleZ);
        //Debug.Log(ballDirection + " " + Arrow.up);      
        //ballDirection = Arrow.up;

        // По углу задаем направление
        angleZ *= Mathf.Deg2Rad;
        ballDirection.x = -Mathf.Sin(angleZ);
        ballDirection.y = Mathf.Cos(angleZ);
    }

    void MiniBallsTrajectoryСorrection()
    {
        if (!Mathf.Approximately(angleZ, prevAngleZ))
        {
            prevAngleZ = angleZ;

            BallController startBall = null;
            BallController minBall = null;

            startBall = objectsPool.GetObject();

            // ballsTrajectory.Min() - cамый нижний элемента, от которого генерируем траекторию полета leadBall
            minBall = (ballsTrajectory.Count > 0) ? ballsTrajectory.Min() : nextBall;

            startBall.rectTransform.localPosition = minBall.rectTransform.localPosition;
            startBall.ballImage.sprite = leadBall.ballImage.sprite;
            startBall.rectTransform.localScale = gmp.sizeAndShiftSmallBall;

            // Проекция на новый вектор
            float length = (startBall.rectTransform.localPosition - Arrow.localPosition).magnitude;
            startBall.rectTransform.localPosition = Arrow.localPosition + ballDirection * length;

            if (length > startBall.rectTransform.localScale.x * 100f * 2f)
            {
                startBall.rectTransform.localPosition += -ballDirection * startBall.rectTransform.localScale.x * 100f * gmp.sizeAndShiftSmallBall.z;
            }

            startPosition = startBall.rectTransform.localPosition;
            startBall.direction = ballDirection;

            EraseMiniBallTrajectoryList();
            ballsTrajectory.AddLast(startBall);
            BuildTrajectory(startBall);
        }
    }

    void EraseMiniBallTrajectoryList()
    {
        while (ballsTrajectory.Count > 0)
        {           
            ballsTrajectory.Last.Value.ballImage.sortingOrder = -2; //*
            objectsPool.ReturnObject(ballsTrajectory.Last.Value);
            ballsTrajectory.RemoveLast();
        }
    }

    void BuildTrajectory(BallController prevBall)
    {
        BallController currentBall = null;

        // Пока не достигли target или верхней границы
        bool isFinish = false;

        prevBall.ballImage.sortingOrder = -3; //

        // Цикл добавляет мячи
        while (!isFinish)
        {
            currentBall = objectsPool.GetObject();
            ballsTrajectory.AddLast(currentBall);

            currentBall.ballImage.sortingOrder = -3; //
            currentBall.rectTransform.localScale = gmp.sizeAndShiftSmallBall;
            currentBall.ballImage.sprite = leadBall.ballImage.sprite;

            currentBall.rectTransform.localPosition = prevBall.rectTransform.localPosition +
                prevBall.direction * currentBall.rectTransform.localScale.x * 100f * gmp.sizeAndShiftSmallBall.z;

            currentBall.direction = prevBall.direction;
            prevBall = currentBall;

            CheckPosition(currentBall, false);

            isFinish = CheckCollision(currentBall);

            if (isFinish)
            {
                // Удаляем 1 элемент вышедшый за границы, и 2 элемент для правильной формации цепочки
                for (int i = 0; i < 2; i++)
                {
                    currentBall = ballsTrajectory.Last.Value;
                    endPosition = currentBall.rectTransform.localPosition;
                    objectsPool.ReturnObject(currentBall);
                    ballsTrajectory.RemoveLast();
                }
            }
        }
    }

    #endregion


#if UNITY_EDITOR

    private void OnApplicationQuit()
    {
        if (leadBall == null || nextBall == null || balls == null)
            return;

        // Упаковка необходимых данных для сериализации

        List<GameData> data = new List<GameData>();
      
        foreach (var item in balls)
        {
            data.Add(item.Value);
        }

        GameDataSerialize dataSerialize = new GameDataSerialize();
        dataSerialize.gameDatas = data.ToArray();

        dataController?.SaveData(dataSerialize, FileType.data);
    }

#elif UNITY_ANDROID

    private void OnApplicationPause()
    {
        if(balls == null)
        {
            return;
        }
       
        // Упаковка необходимых данных для сериализации

        List<GameData> data = new List<GameData>();
 
        foreach (var item in balls)
        {
            data.Add(item.Value);
        }

        GameDataSerialize dataSerialize = new GameDataSerialize();
        dataSerialize.gameDatas = data.ToArray();

        dataController?.SaveData(dataSerialize, FileType.data);
    }

#endif

    BallController DeploymentGameData(GameData gameDatas)
    {
        BallController ballController = objectsPool.GetObject();

        ballController.id = gameDatas.id;
        ballController.ballImage.sprite = bubbleSprites[gameDatas.ballType - 1];
        ballController.ballType = gameDatas.ballType;
        Vector2 localScale = new Vector2(ballSize, ballSize);
        ballController.rectTransform.localScale = localScale;

        // По id определяем позицию

        Vector2 position = new Vector2();
        float half_size = ballSize / 2f;

        int i = gameDatas.id / gmp.quantity_y;
        int j = gameDatas.id % gmp.quantity_y;

        position.x = half_size + j * ballSize;
        position.y = -half_size + i * -ballSize;

        if ((i & 1) != 0)
        {
            position.x += half_size;
        }

        ballController.rectTransform.localPosition = position;

        return ballController;
    }

    void LoadData()
    {
        GameDataSerialize data = dataController?.LoadData<GameDataSerialize>(FileType.data);

        // Если есть данные продолжаем игру
        if (data != null && data.gameDatas != null && data.gameDatas.Length > 0)
        {
            BallController ballController;

            for (int i = 0; i < data.gameDatas.Length; i++)
            {
                ballController = DeploymentGameData(data.gameDatas[i]);
                ballController.rectTransform.localScale = new Vector2(ballSize / 100f, ballSize / 100f);
                balls.Add(ballController.id, ballController);
            }

            InitializeBalls();
        }
        else
        {
            StartCoroutine(AddRow(quantity_x, InitializeBalls));
        }
    }

    void ResetData()
    {
        if (nextBall != null)
        {
            objectsPool.ReturnObject(nextBall);
            nextBall = null;
        }

        if (leadBall != null)
        {
            leadBall.trailRenderer.enabled = false;
            objectsPool.ReturnObject(leadBall);
            leadBall = null;
        }

        if(balls.Count > 0)
        {
            foreach (var item in balls)
            {
                objectsPool.ReturnObject(item.Value);
            }

            balls.Clear();
        }

        quantity_x = gmp.quantity_x;
    }

    private void Start()
    {
        InitializeParams();
        InitializeObjectsPool();

        dataController = new DataController(gameEvents);
        dataController.LoadPlayerProgress();
        LoadData();

        var seed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
        UnityEngine.Random.InitState(seed);
    }
   

    private void OnEnable()
    {
        if(gameState == GameState.NewGame)
        {
            ResetData();
            StartCoroutine(AddRow(quantity_x, InitializeBalls));
        }
    }

    void Update()
    {
        if (gameState.Equals(GameState.None))
        {
            return;
        }
        
        // Если установлено состояние GameState.Aiming и клик был совершен не по кнопке
        else if (gameState.Equals(GameState.Aiming) && EventSystem.current.currentSelectedGameObject == null)
        {
            if (Input.GetMouseButton(0)) 
            {
                SetTrajectoryFlight();

                // Анимация траектории полета
                foreach (var item in ballsTrajectory)
                {
                    item.rectTransform.localPosition += item.direction * Time.deltaTime * 100f * gmp.speedSmallBall;
                    CheckPosition(item);
                }
            }

            if (Input.GetMouseButtonUp(0))
            {
                AudioManager.Instance.PlaySound("Throw");
                leadBall.direction = ballDirection;
                EraseMiniBallTrajectoryList();
                gameState = GameState.BubbleFlight;
            }
        }

        else if (gameState.Equals(GameState.BubbleFlight))
        {
            leadBall.rectTransform.localPosition += leadBall.direction * Time.deltaTime * 100f * gmp.speedLeadBall;
            CheckCollision();
        }
    }

    /// <summary>
    /// /////////////////////////////////////////////////////////////////////////////////////////////////////
    /// </summary>

    Dictionary<int, bool> used = new Dictionary<int, bool>();
    LinkedList<int> comp = new LinkedList<int>();
    LinkedList<int> listSingleColor = new LinkedList<int>();
    LinkedList<int> listOfIsolated = new LinkedList<int>();

#region Удаление однотипных

    bool CheckBounds(int i, int j)
    {
        if (i < 0 || i >= quantity_x)
        {
            return false;
        }
        if (j < 0 || j >= gmp.quantity_y)
        {
            return false;
        }

        return true;
    }

    bool Check(int i, int j, int v, int destroy)
    {
        bool isBounds = CheckBounds(i, j);
        return isBounds && balls.ContainsKey(v) && balls[v].ballType == destroy;  // помечаем конкретный
    }

    void DFS(int i, int j, int destroy)
    {
        int v = i * gmp.quantity_y + j;

        if (!Check(i, j, v, destroy)) return;

        balls[v].ballType = -balls[v].ballType; // Помечаем вершину как "Пройденная"

        listSingleColor.AddLast(v);

        DFS(i - 1, j, destroy); // top       
        DFS(i, j + 1, destroy); // right 
        DFS(i + 1, j, destroy); // bottom       
        DFS(i, j - 1, destroy); // left

        if ((i & 1) != 0)
        {
            DFS(i - 1, j + 1, destroy); // top-right
            DFS(i + 1, j + 1, destroy); // bottom-right  
        }
        else
        {
            DFS(i - 1, j - 1, destroy); // top-left     
            DFS(i + 1, j - 1, destroy); // bottom-left      
        }
    }

#endregion

#region Поиск изолированных вершин

    bool CheckVertex(int i, int j, int v)
    {
        bool isBounds = CheckBounds(i, j);
        return isBounds && (balls.ContainsKey(v) && balls[v].ballType > 0) && !used[v];  // если вершина достижима и не за границами
    }

    void DFS(int i, int j)
    {
        int v = i * gmp.quantity_y + j;

        if (!CheckVertex(i, j, v)) return;

        used[v] = true;
        comp.AddLast(v);

        DFS(i - 1, j); // top       
        DFS(i, j + 1); // right 
        DFS(i + 1, j); // bottom       
        DFS(i, j - 1); // left

        if (((i + 1) & 1) == 0)
        {
            DFS(i - 1, j + 1); // top-right
            DFS(i + 1, j + 1); // bottom-right 
        }
        else
        {
            DFS(i - 1, j - 1); // top-left     
            DFS(i + 1, j - 1); // bottom-left      
        }
    }

    void HighlightingIsolated()
    {
        int i, j;

        // ball.Value.id - номер вершины
        // i, j - координаты вершины  ball.Value.id

        foreach (KeyValuePair<int, BallController> ball in balls)
        {
            if (ball.Value.ballType > 0)
            {
                used.Add(ball.Value.id, false);
            }
        }

        // Поиск изолированных
        foreach (KeyValuePair<int, BallController> ball in balls)
        {
            i = ball.Value.id / gmp.quantity_y;
            j = ball.Value.id % gmp.quantity_y;

            int v = i * gmp.quantity_y + j;

            // Если balls[v].ballType меньше нуля, то логически он отсутствует на поле и не должен обрабатываться

            if ((balls.ContainsKey(v) && balls[v].ballType > 0) && !used[ball.Value.id])
            {
                comp.Clear();
                DFS(i, j);

                if (i != 0) // Если элемент не связан с нулевой строчкой, то он не относится к корневому
                {
                    // Copy
                    LinkedListNode<int> current = comp.First;
                    while (current != null)
                    {
                        listOfIsolated.AddLast(current.Value);
                        current = current.Next;
                    }
                }
            }
        }

        used.Clear();
    }

#endregion
}
