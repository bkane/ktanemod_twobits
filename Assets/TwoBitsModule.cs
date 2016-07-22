﻿using Newtonsoft.Json;
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
    public KMSelectable[] Buttons;
    public KMSelectable QueryButton;
    public KMSelectable SubmitButton;

    public AudioClip CharacterEntryClip;
    public AudioClip ProcessingClip;

    public int NUM_ITERATIONS = 3;

    public float TimeWorking = 5f;
    public float TimeShowingResult = 5f;
    public float TimeError = 5f;
    public float TimeSubmitting = 5f;

    protected static char[] buttonLabels = new char[] { 'b', 'c', 'd', 'e', 'g', 'k', 'p', 't', 'v', 'z' };
    protected static List<char> alphabet = new List<char>() { 'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't', 'u', 'v', 'w', 'x', 'y', 'z' };

    protected string ERROR_STRING = "ERROR";
    protected string INCORRECT_SUBMISSION_STRING = "INCORRECT";

    protected KMBombInfo bombInfo;
    protected KMBombModule module;
    protected KMAudio kmAudio;
    protected State currentState;
    protected char[] currentQuery;
    protected int firstQueryCode;
    protected int lastResult;

    Dictionary<string, int> queryResponses;
    Dictionary<int, string> queryLookups;

    void Awake()
    {
        bombInfo = gameObject.AddComponent<KMBombInfo>();

        kmAudio = GetComponent<KMAudio>();
        module = GetComponent<KMBombModule>();
        module.OnActivate += OnActivate;

        for (int i = 0; i < Buttons.Length; i++)
        {
            int buttonIndex = i;
            Buttons[i].OnInteract += delegate () {
                OnButtonPress(buttonIndex);
                return false;
            };
        }

        QueryButton.OnInteract += OnQuery;
        SubmitButton.OnInteract += OnSubmit;

        currentState = State.Inactive;
        currentQuery = new char[2];

        CreateRules();

        firstQueryCode = CalculateFirstQueryCode();

        UpdateDisplay();
    }

    protected void OnActivate()
    {
        ChangeState(State.Idle);
    }

    protected void OnButtonPress(int buttonIndex)
    {
        switch (currentState)
        {
            case State.Inactive:
                {
                    //Don't perform any real logic or change states if the module isn't active yet
                    module.HandleStrike();
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
                            kmAudio.PlaySoundAtTransform(CharacterEntryClip.name, transform);
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
        switch (currentState)
        {
            case State.Inactive:
                {
                    //Don't perform any real logic or change states if the module isn't active yet
                    module.HandleStrike();
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
        switch (currentState)
        {
            case State.Inactive:
                {
                    //Don't perform any real logic or change states if the module isn't active yet
                    module.HandleStrike();
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
                    kmAudio.PlaySoundAtTransform(ProcessingClip.name, transform);
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
                    module.HandleStrike();
                    UpdateDisplay();
                    StartCoroutine(FlashErrorCoroutine(INCORRECT_SUBMISSION_STRING));
                }
                break;
            case State.Complete:
                {
                    module.HandlePass();
                    UpdateDisplay();
                }
                break;
        }
    }

    protected void HandleError()
    {
        module.HandleStrike();
        ChangeState(State.ShowingError);
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

    protected int CalculateFirstQueryCode()
    {
        //Batteries
        int batteryCount = 0;
        List<string> batteryResponses = bombInfo.QueryWidgets(KMBombInfo.QUERYKEY_GET_BATTERIES, null);
        foreach (string response in batteryResponses)
        {
            Dictionary<string, int> responseDict = JsonConvert.DeserializeObject<Dictionary<string, int>>(response);
            batteryCount += responseDict["numbatteries"];
        }


        //Serial Number
        int serialFirstLetterModifier = 0;
        string serialNumber = "0";
        List<string> serialResponses = bombInfo.QueryWidgets(KMBombInfo.QUERYKEY_GET_SERIAL_NUMBER, null);
        if (serialResponses.Count > 0)
        {
            serialNumber = JsonConvert.DeserializeObject<Dictionary<string, string>>(serialResponses[0])["serial"];
        }
        
        for (int i = 0; i < serialNumber.Length; i++)
        {
            int index = alphabet.IndexOf(serialNumber[i]);
            if (index >= 0)
            {
                serialFirstLetterModifier = index;
                break;
            }
        }

        int serialLastDigit = int.Parse(serialNumber[serialNumber.Length - 1].ToString());


        //Ports
        bool hasStereoRCAPort = false;
        bool hasRJ45Port = false;
        List<string> portResponses = bombInfo.QueryWidgets(KMBombInfo.QUERYKEY_GET_PORTS, null);
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

        return code % 100;
    }

    protected string CalculateCorrectSubmission()
    {
        int queryInt = firstQueryCode;
        string queryString = string.Empty;

        for (int i = 0; i <= NUM_ITERATIONS; i++)
        {
            queryString = queryLookups[queryInt];
            queryInt = queryResponses[queryString];
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