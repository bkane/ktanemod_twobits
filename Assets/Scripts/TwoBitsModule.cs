using System;
using Newtonsoft.Json;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class TwoBitsModule : MonoBehaviour
{
    protected enum State
    {
        Inactive,
        Idle,
        Working,
        ShowingResult,
        ShowingError,
        SubmittingResult,
        IncorrectSubmission,
        Complete
    }

    public TextMesh DisplayText;
    public Button[] Buttons;
    public Button QueryButton;
    public Button SubmitButton;

    public KMAudio KMAudio;
    public KMBombInfo BombInfo;

    public int NUM_ITERATIONS = 3;

    public float TimeWorking = 5f;
    public float TimeShowingResult = 5f;
    public float TimeError = 5f;
    public float TimeSubmitting = 5f;

    protected static char[] buttonLabels = new char[] { 'b', 'c', 'd', 'e', 'g', 'k', 'p', 't', 'v', 'z' };
    protected static List<char> alphabet = new List<char>() { 'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'M', 'N', 'O', 'P', 'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z' };

    protected string ERROR_STRING = "ERROR";
    protected string INCORRECT_SUBMISSION_STRING = "INCORRECT";

    protected KMBombModule module;
    
    protected State currentState;
    protected char[] currentQuery;
    protected int firstQueryCode;
    protected int lastResult;
    protected bool twitchPlayStrike;

    Dictionary<string, int> queryResponses;
    Dictionary<int, string> queryLookups;

    private static int _moduleIdCounter = 1;
    private int _moduleId;

    void Awake()
    {
        _moduleId = _moduleIdCounter++;

        module = GetComponent<KMBombModule>();
        module.OnActivate += OnActivate;

        for (int i = 0; i < Buttons.Length; i++)
        {
            int buttonIndex = i;
            Buttons[i].Selectable.OnInteract += delegate ()
            {
                OnButtonPress(buttonIndex);
                return false;
            };
        }

        QueryButton.Selectable.OnInteract += OnQuery;
        SubmitButton.Selectable.OnInteract += OnSubmit;

        currentState = State.Inactive;
        currentQuery = new char[2];

        UpdateDisplay();
    }

    protected void OnActivate()
    {
        CreateRules();

        firstQueryCode = CalculateFirstQueryCode();

        
        Debug.LogFormat("[Two Bits #{1}] Starting code is {0}", firstQueryCode, _moduleId);
        var correct = CalculateCorrectSubmission(true);
        Debug.LogFormat("[Two Bits #{1}] Correct Submission response is {0}", correct, _moduleId);

        ChangeState(State.Idle);
    }

    protected void OnButtonPress(int buttonIndex)
    {
        Buttons[buttonIndex].Push();

        switch (currentState)
        {
            case State.Inactive:
                {
                    //Don't perform any real logic or change states if the module isn't active yet
                    HandleError();
                }
                break;
            case State.Complete:
                {
                    //do nothing
                }
                break;
            case State.Idle:
                {
                    bool validEntry = false;
                    for (int i = 0; i < currentQuery.Length; i++)
                    {
                        if (currentQuery[i] == '_')
                        {
                            validEntry = true;
                            currentQuery[i] = buttonLabels[buttonIndex];
                            break;
                        }
                    }

                    if (validEntry)
                    {
                        UpdateDisplay();
                    }
                    else
                    {
                        HandleError();
                    }
                }
                break;
            default:
                {
                    //Unforgiving!
                    HandleError();
                }
                break;
        }
    }

    protected bool OnQuery()
    {
        QueryButton.Push();
        switch (currentState)
        {
            case State.Inactive:
                {
                    //Don't perform any real logic or change states if the module isn't active yet
                    HandleError();
                }
                break;
            case State.Complete:
                {
                    //do nothing
                }
                break;
            case State.Idle:
                {
                    bool queryOK = true;

                    for (int i = 0; i < currentQuery.Length; i++)
                    {
                        if (!buttonLabels.Contains(currentQuery[i]))
                        {
                            HandleError();
                            queryOK = false;
                            break;
                        }
                    }

                    if (queryOK)
                    {
                        ChangeState(State.Working);
                    }
                }
                break;
            default:
                {
                    //Unforgiving!
                    HandleError();
                }
                break;
        }

        return false;
    }

    protected bool OnSubmit()
    {
        SubmitButton.Push();
        switch (currentState)
        {
            case State.Inactive:
                {
                    //Don't perform any real logic or change states if the module isn't active yet
                    HandleError();
                }
                break;
            case State.Complete:
                {
                    //do nothing
                }
                break;
            case State.Idle:
                {
                    ChangeState(State.SubmittingResult);
                }
                break;
            default:
                {
                    HandleError();
                }
                break;
        }

        return false;
    }

    protected void ChangeState(State state)
    {
        if (!gameObject.activeInHierarchy)
        {
            return;
        }

        StopAllCoroutines();
        currentState = state;

        switch (currentState)
        {
            case State.Idle:
                {
                    currentQuery[0] = '_';
                    currentQuery[1] = '_';
                    UpdateDisplay();
                }
                break;
            case State.Working:
                {
                    UpdateDisplay();
                    StartCoroutine(DelayedStateChangeCoroutine(TimeWorking, State.ShowingResult));
                }
                break;
            case State.ShowingResult:
                {
                    string currentQueryString = GetCurrentQueryString();

                    if (queryResponses.ContainsKey(currentQueryString))
                    {
                        lastResult = queryResponses[currentQueryString];
                    }

                    UpdateDisplay();
                    StartCoroutine(DelayedStateChangeCoroutine(TimeShowingResult, State.Idle));
                }
                break;
            case State.ShowingError:
                {
                    UpdateDisplay();
                    StartCoroutine(FlashErrorCoroutine(ERROR_STRING));
                }
                break;
            case State.SubmittingResult:
                {
                    KMAudio.PlaySoundAtTransform("processing", transform);
                    UpdateDisplay();

                    if (CalculateCorrectSubmission().Equals(GetCurrentQueryString()))
                    {
                        StartCoroutine(DelayedStateChangeCoroutine(TimeSubmitting, State.Complete));
                    }
                    else
                    {
                        StartCoroutine(DelayedStateChangeCoroutine(TimeSubmitting, State.IncorrectSubmission));
                    }
                }
                break;
            case State.IncorrectSubmission:
                {
                    HandleError();
                }
                break;
            case State.Complete:
                {
                    Debug.LogFormat("[Two Bits #{0}] Module solved",_moduleId);
                    module.HandlePass();
                    UpdateDisplay();
                }
                break;
        }
    }

    protected void HandleError()
    {
        twitchPlayStrike = true;
        module.HandleStrike();
        switch (currentState)
        {
            case State.Inactive:
                Debug.LogFormat("[Two Bits #{0}] Pressed a button while the module was sleeping", _moduleId);
                break;
            case State.IncorrectSubmission:
                Debug.LogFormat("[Two Bits #{0}] Submitted {1}, Expected {2}", _moduleId, GetCurrentQueryString(),
                    CalculateCorrectSubmission());
                UpdateDisplay();
                StartCoroutine(FlashErrorCoroutine(INCORRECT_SUBMISSION_STRING));
                break;
            case State.Idle:
                if (GetCurrentQueryString().Contains('_'))
                {
                    Debug.LogFormat("[Two Bits #{0}] Queried incomplete input {1}", _moduleId, GetCurrentQueryString());
                }
                else
                {
                    Debug.LogFormat("[Two Bits #{0}] Pressed a button other than Query or Submit");
                }
                ChangeState(State.ShowingError);
                break;
            default:
                Debug.LogFormat("[Two Bits #{0}] Pressed a button while the module was working",_moduleId);
                ChangeState(State.ShowingError);
                break;
        }
    }

    protected void CreateRules()
    {
        //Grid mapping 00-99 to all possible letter combinations
        queryLookups = new Dictionary<int, string>();

        //Mapping of all letter combinations to 00-99
        queryResponses = new Dictionary<string, int>();

        List<int> responses = new List<int>();
        List<string> queries = new List<string>();
        for (int i = 0; i < 100; i++)
        {
            responses.Add(i);
            queries.Add(string.Format("{0}{1}", buttonLabels[i / 10], buttonLabels[i % 10]));
        }

        System.Random rand = new System.Random(0);
        responses.Shuffle<int>(rand);

        for (int i = 0; i < 100; i++)
        {
            queryLookups.Add(responses[i], queries[i]);
        }

        //Now make the responses differ on a per-module basis
        rand = new System.Random(UnityEngine.Random.Range(0, int.MaxValue));
        queries.Shuffle<string>(rand);

        for (int i = 0; i < 100; i++)
        {
            queryResponses.Add(queries[i], responses[i]);
        }

        string grid = "<table><tbody><tr><th>  </th>";
        for (int i = 0; i < 10; i++)
        {
            grid += "<th>-" + i + "</th>";
        }

        grid += "</tr>\n";

        for (int i = 0; i < 10; i++)
        {
            string line = "<tr><th>" + i + "-</th>";

            for (int j = 0; j < 10; j++)
            {
                line += "<td>" + queryLookups[i * 10 + j] + "</td>";
            }

            grid += line + "</tr>\n";
        }

        grid += "</tbody></table>";

        Debug.LogFormat("Lookup grid:\n{0}", grid);

        Debug.LogFormat("QueryLookups: {0}", string.Join("\n", queryLookups.Keys.Select(i => string.Format("[{0}]: {1}", i, queryLookups[i])).ToArray()));
        Debug.LogFormat("QueryResponses: {0}", string.Join("\n", queryResponses.Keys.Select(s => string.Format("[{0}]: {1}", s, queryResponses[s])).ToArray()));
    }

    protected IEnumerator ProcessTwitchCommand(string command)
    {
        twitchPlayStrike = false;
        var split = command.ToLowerInvariant().Split(new[] {" "}, StringSplitOptions.RemoveEmptyEntries);

        if (split.Length < 2 || split[0] != "press")
            yield break;

        foreach (var x in split.Skip(1))
        {
            switch (x)
            {
                case "query":
                case "submit":
                    break;
                default:
                    foreach (var y in x)
                        if (!buttonLabels.Contains(y))
                            yield break;
                    break;
            }
        }

        yield return "Two Bits Solve Attempt";
        foreach (var x in split.Skip(1))
        {
            switch (x)
            {
                case "query":
                    OnQuery();
                    break;
                case "submit":
                    OnSubmit();
                    break;
                default:
                    foreach (var y in x)
                    {
                        OnButtonPress("bcdegkptvz".IndexOf(y));
                        if (twitchPlayStrike)
                            yield break;
                    }
                    break;
            }
            yield return new WaitForSeconds(0.1f);
        }

        if(currentState == State.SubmittingResult)
            yield return GetCurrentQueryString().Equals(CalculateCorrectSubmission())
                        ? "solve"
                        : "strike";
    }

    protected int CalculateFirstQueryCode()
    {
        //Batteries
        int batteryCount = 0;
        List<string> batteryResponses = BombInfo.QueryWidgets(KMBombInfo.QUERYKEY_GET_BATTERIES, null);
        foreach (string response in batteryResponses)
        {
            Dictionary<string, int> responseDict = JsonConvert.DeserializeObject<Dictionary<string, int>>(response);
            batteryCount += responseDict["numbatteries"];
        }


        //Serial Number
        int serialFirstLetterModifier = 0;
        string serialNumber = "0";
        List<string> serialResponses = BombInfo.QueryWidgets(KMBombInfo.QUERYKEY_GET_SERIAL_NUMBER, null);
        if (serialResponses.Count > 0)
        {
            serialNumber = JsonConvert.DeserializeObject<Dictionary<string, string>>(serialResponses[0])["serial"];
        }
        
        for (int i = 0; i < serialNumber.Length; i++)
        {
            int index = alphabet.IndexOf(serialNumber[i]);
            if (index >= 0)
            {
                serialFirstLetterModifier = index + 1;
                break;
            }
        }

        int serialLastDigit = int.Parse(serialNumber[serialNumber.Length - 1].ToString());


        //Ports
        bool hasStereoRCAPort = false;
        bool hasRJ45Port = false;
        List<string> portResponses = BombInfo.QueryWidgets(KMBombInfo.QUERYKEY_GET_PORTS, null);
        foreach (var response in portResponses)
        {
            var responseDict = JsonConvert.DeserializeObject<Dictionary<string, List<string>>>(response);
            if (responseDict["presentPorts"].Contains("StereoRCA"))
            {
                hasStereoRCAPort = true;
            }

            if (responseDict["presentPorts"].Contains("RJ45"))
            {
                hasRJ45Port = true;
            }
        }

        int code = serialFirstLetterModifier + (serialLastDigit * batteryCount);

        if (hasStereoRCAPort && !hasRJ45Port)
        {
            code *= 2;
        }

        code = code % 100;

        return code;
    }

    protected string CalculateCorrectSubmission(bool DebugLog = false)
    {
        int queryInt = firstQueryCode;
        string queryString = string.Empty;

        for (int i = 0; i <= NUM_ITERATIONS; i++)
        {
            queryString = queryLookups[queryInt];
            queryInt = queryResponses[queryString];
            if (DebugLog && i < NUM_ITERATIONS)
                Debug.LogFormat("[Two Bits #{0}] Query #{1}: {2}, Response: {3}", _moduleId, i + 1, queryString,
                    queryInt);
        }

        return queryString;
    }

    protected IEnumerator DelayedStateChangeCoroutine(float delay, State nextState)
    {
        yield return new WaitForSeconds(delay);
        ChangeState(nextState);
    }

    protected IEnumerator FlashErrorCoroutine(string message)
    {
        DisplayText.text = message;
        yield return new WaitForSeconds(TimeError);
        DisplayText.text = "";
        yield return new WaitForSeconds(0.35f);
        DisplayText.text = message;
        yield return new WaitForSeconds(TimeError);
        DisplayText.text = "";
        yield return new WaitForSeconds(0.35f);
        DisplayText.text = message;
        yield return new WaitForSeconds(TimeError);
        ChangeState(State.Idle);
    }

    protected string GetCurrentQueryString()
    {
        return string.Format("{0}{1}", currentQuery[0], currentQuery[1]);
    }

    protected void UpdateDisplay()
    {
        switch (currentState)
        {
            case State.Idle:
                {
                    DisplayText.text = string.Format("{0} {1}", currentQuery[0], currentQuery[1]);
                }
                break;
            case State.Working:
                {
                    DisplayText.text = "Working...";
                }
                break;
            case State.ShowingResult:
                {
                    DisplayText.text = string.Format("Result: {0}{1}", lastResult / 10, lastResult % 10);
                }
                break;
            case State.ShowingError:
                {
                    DisplayText.text = ERROR_STRING;
                }
                break;
            case State.SubmittingResult:
                {
                    DisplayText.text = "SUBMITTING";
                }
                break;
            case State.IncorrectSubmission:
                {
                    DisplayText.text = INCORRECT_SUBMISSION_STRING;
                }
                break;
            case State.Complete:
                {
                    DisplayText.text = "CORRECT";
                }
                break;
            case State.Inactive:
                {
                    DisplayText.text = string.Empty;
                }
                break;
        }
    }
}
