using KeepCoding;
using RNG = UnityEngine.Random;
using KModkit;
using System;
using System.Linq;
using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using System.Text.RegularExpressions;

public class threeInputLogicGatesScript : ModuleScript {
	public StandWrapper[] oS; //Because Unity inspector is peepee poopoo
	private StandWrapper[][] outerStands;

	public StandWrapper[] innerStands; 
	public StandWrapper centerStand;

	private StandWrapper[] allStands;

	private bool[,] outerValues;
	private bool[] innerValues;
	private Tuple<int, int>[] innerOrder;
	private bool centerValue;

	public Material[] materials; //unlit b,lit b,unlit g,lit g

	private byte[] innerOperators;
	private byte centerOperator;

	private static readonly string operatorSymbols = @"∅123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz/Π+(){}[]<>,;:!?$£€§%*#~^&@°µ☺☻▲▶◀▼♓♒♑♐♏♎♍♌♋♊♉♈☉☽☿♀⊕♁♂♃♄♅♆♇✔✘♣♦♥♠♡♢♤♧♔♕♖♗♘♙♚♛♜♝♞♟½⅓¼αβγδεζηθικλμνξοπρςστυφχψω⊞⊟⊠⊡⊖⊘⊗◧◨◩◪△▷▽◁◢◣◤◥◭◮☮☯☀☁☂☃❄☎✈✉☹❤☢☣⌘☠★☆✦✧✿❀↖↗↘↙↞↟↠↡∑Ω※‽￠㊋㊌㊍㊎㊏±=≠☄∞☸ѼѬѮ◆◇❣₩¥✡✯☜☝☞☟⁂♭☤❖";

	private byte stageNumber = 0;

	private List<bool> expectedValues;

	// Use this for initialization
	void Start () {
		outerStands = new StandWrapper[3][];
		//Log(operatorSymbols.GroupBy(w => w).Where(w => w.Count() > 1).Count());
		//for (int i = 0; i < operatorSymbols.Length; i++) Log("{0} {1}", i, operatorSymbols[i].ToString());
		for(int i = 0; i < 3; i++)
        {
			outerStands[i] = oS.Skip(i*3).Take(3).ToArray().Shuffle();
			KMSelectable[] arrayOfStuff = outerStands[i].Select(os => os.selectable).ToArray();
			int tmp = i;
			arrayOfStuff.ForEach(km=>km.Assign(onInteract: ()=>OuterBulbPress(tmp,arrayOfStuff.IndexOf(km)))) ;
			for(int j = 0; j < 3; j++)
            {
				outerStands[i][j].textMesh.text = j.ToString() ;
            }
        }
		innerOrder = new Tuple<int, int>[3] { new Tuple<int, int>(0, Get<KMBombInfo>().GetBatteryCount()), new Tuple<int, int>(1, Get<KMBombInfo>().GetIndicators().Count()), new Tuple<int, int>(2, Get<KMBombInfo>().GetPortCount()) };
		//Log(innerOrder.Select(t => t.Item2));
		innerOrder = innerOrder.OrderBy((t1, t2) => t1.Item2.CompareTo(t2.Item2)).ToArray();
		//Log(innerOrder);
		Log("The order of the inner variables (LSB to MSB) are : {0}",innerOrder.Select(t => (t.Item1+1).ToOrdinal()));
		allStands = oS.Concat(innerStands).Concat(centerStand).ToArray();
		innerStands.Select(iS => iS.selectable).ToArray().Assign(onInteract: InnerBulbPress);
		centerStand.selectable.Assign(onInteract: CenterPress);
		outerValues = new bool[3, 3];
		innerValues = new bool[3];
		innerOperators = new byte[3];
		centerValue = false;
		SetUpStage();
	}

    private void CenterPress()
    {
		ButtonEffect(centerStand.selectable,.2f, "tap");
		bool correct;
        switch (stageNumber)
        {
			case 0:
				bool userInput = Math.Floor(Get<KMBombInfo>().GetTime()) % 2 == 0;
				correct = userInput == expectedValues[0];
				Log("Center value given : {0}.", userInput);
				break;
			case 1:
				Log("Inner values given : {0}.", innerValues);
				correct = innerValues.SequenceEqual(expectedValues.ToArray());
				break;
			case 2:
				Log("Outer values given : {0}.", outerValues);
				correct = innerValues.SequenceEqual(CalculateInner().ToArray());
				break;
			default:
				return;
		}
		if (correct)
		{
			if (++stageNumber == 3)
			{
				allStands.ForEach(s => { s.meshRenderer.material = materials[3]; s.textMesh.text = "!"; });
				Log("Module solved.");
				PlaySound(KMSoundOverride.SoundEffect.CorrectChime);
				Solve();
			}
			else SetUpStage();
		}
		else
		{
			Log("This is wrong. Strike!");
			Strike();
		}
	}

    private void OuterBulbPress(int section,int index)
    {
		ButtonEffect(outerStands[section][index].selectable, .1f, "tap");
		if (stageNumber != 2) return;
		outerValues[section,index] = !outerValues[section,index];
		MaterialChange(outerStands[section][index].meshRenderer, stageNumber >= 1, outerValues[section, index]);
    }

	private void InnerBulbPress(int obj)
	{
		ButtonEffect(innerStands[obj].selectable, .1f, "tap");
		if (stageNumber != 1) return;
		innerValues[obj] = !innerValues[obj];
		MaterialChange(innerStands[obj].meshRenderer, stageNumber >= 2, innerValues[obj]);
	}

	private void MaterialChange(MeshRenderer mr, bool isStageCompleted ,bool isTrue)
    {
		mr.material= materials[(isStageCompleted ? 2 : 0) + (isTrue ? 1 : 0)];
	}

	private void SetUpStage()
    {
		Log("Starting stage n°{0}:", stageNumber + 1);
		GetNewOperators();
        switch (stageNumber)
        {
			case 0:
				SetUpOuter();
				SetUpInner();
				expectedValues = CalculateCenter();
				Log("Outer values are : {0}.", outerValues);
				Log("Inner values are : {0}.", innerValues);
				Log("The center value should be : {0}.", expectedValues);
				break;
			case 1:
				SetUpOuter();
				expectedValues = CalculateInner();
				Log("Outer values are : {0}.", outerValues);
				Log("Center value is : {0}.", centerValue);
				Log("The inner values should be : {0}.", expectedValues.Join(","));
				SetUpCenter(expectedValues.ToArray());
				break;
			case 2:
				SetUpInner(true);
				SetUpCenter(innerValues);
				expectedValues = FindOuterSolution();
				Log("Inner values are : {0}.", innerValues);
				Log("Center value is : {0}.", centerValue);
				Log("The outer values can be : {0}.", expectedValues.Join(","));
				break;
		}
		
	}

	private void SetUpOuter()
    {
		for (int i = 0; i < 3; i++)
		{
			for (int j = 0; j < 3; j++)
			{
				outerValues[i, j] = Helper.RandomBoolean();
				MaterialChange(outerStands[i][j].meshRenderer, stageNumber >= 1, outerValues[i, j]);
			}
		}
	}

	private List<bool> FindOuterSolution()
    {
		List<bool> res = new List<bool>();
		for(int i = 0; i < 3; i++)
        {
			string op = Helper.Base(innerOperators[i].ToString(), 10, 2).PadLeft(8, '0');
			int lowest = op.IndexOf(innerValues[i] ? '1' : '0');
			List<bool> tmp = new List<bool>();
			for (int j = 2; j >= 0; j--)
            {
				if (lowest >= (int)Math.Pow(2, j))
				{
					lowest -= (int)Math.Pow(2, j);
					tmp.Add(true);
				}
				else tmp.Add(false);
            }
			tmp.Reverse();
			res.AddRange(tmp);
		}
		return res;
    } 

	private List<bool> CalculateCenter()
	{
		int truthTableIndex = 0;
		foreach(Tuple<int,int> t in innerOrder) truthTableIndex += innerValues[t.Item1] ? (int)Math.Pow(2, innerOrder.IndexOf(t)) : 0;
		//for (int i = 0; i < 3; i++) truthTableIndex += innerValues[i] ? (int)Math.Pow(2, i):0;
		List<bool> a = new List<bool>();
		a.Add(Helper.Base(centerOperator.ToString(), 10, 2).PadLeft(8, '0')[truthTableIndex] == '1');
		return a;
	}

	private void SetUpCenter(bool[] innerValues)
    {
		int truthTableIndex = 0;
		//for (int i = 0; i < 3; i++) truthTableIndex += innerValues[i] ? (int)Math.Pow(2, i) : 0;
		foreach (Tuple<int, int> t in innerOrder) truthTableIndex += innerValues[t.Item1] ? (int)Math.Pow(2, innerOrder.IndexOf(t)) : 0;
		centerValue = Helper.Base(centerOperator.ToString(), 10, 2).PadLeft(8, '0')[truthTableIndex] == '1';
		MaterialChange(centerStand.meshRenderer, stageNumber == 3, centerValue);
	}

	private List<bool> CalculateInner()
    {
		List<bool> expectedValues = new List<bool>();
		//foreach(int i in innerOrder.Select(x=>x.Item1))
		for (int i = 0; i < 3; i++)
		{
			int truthTableIndex = 0;
			for (int j = 0; j < 3; j++)
			{
				truthTableIndex += outerValues[i, j] ? (int)Math.Pow(2, j) : 0;
			}
			expectedValues.Add(Helper.Base(innerOperators[i].ToString(), 10, 2).PadLeft(8, '0')[truthTableIndex] == '1');
		}
		return expectedValues;
    }
	private void SetUpInner(bool isRandom = false)
    {
		//foreach (int i in innerOrder.Select(x => x.Item1))
		for (int i = 0; i < 3; i++)
        {
			if (!isRandom)
			{
				int truthTableIndex = 0;
				for (int j = 0; j < 3; j++)
				{
					truthTableIndex += outerValues[i, j] ? (int)Math.Pow(2, j) : 0;
				}
				innerValues[i] = Helper.Base(innerOperators[i].ToString(), 10, 2).PadLeft(8, '0')[truthTableIndex] == '1';
			}
			else innerValues[i] = Helper.RandomBoolean();
			MaterialChange(innerStands[i].meshRenderer, stageNumber >= 2, innerValues[i]);
        }
    }

	private void GetNewOperators()
    {
		for (int i = 0; i < 3; i++)
		{
			innerOperators[i] = (byte)RNG.Range(stageNumber==2?1:0,stageNumber==2?255:256);
			innerStands[i].textMesh.text = operatorSymbols[innerOperators[i]].ToString();
			Log("The {0} inner operator (starting from north, going clockwise) is {1}, which is operator n°{2}({3}).", (i + 1).ToOrdinal(), /*innerStands[i].textMesh.text*/operatorSymbols[innerOperators[i]], innerOperators[i], Helper.Base(innerOperators[i].ToString(), 10, 2).PadLeft(8, '0'));
		}
		centerOperator = (byte)RNG.Range(0, 256);
		centerStand.textMesh.text = operatorSymbols[centerOperator].ToString();
		Log("The center operator is {0}, which is operator n°{1}({2}).", centerStand.textMesh.text, centerOperator, Helper.Base(centerOperator.ToString(), 10, 2).PadLeft(8, '0'));
	}

#pragma warning disable 414
	private readonly string TwitchHelpMessage = @"[!{0} toggle|press outer 1 2 3 4 5 6 7 8 9] to toggle the outer bulbs, n°1 being the top-left bulb, then go clockwise. [!{0} toggle|press inner 1 2 3] to toggle the inner bulb, n°1 being the north bulb, then go clockwise. [!{0} press center even/odd] to press the center bulb when the last digit of the bomb is even/odd. Ignore this parameter to press it at any time.";
#pragma warning restore 414

	private IEnumerator ProcessTwitchCommand(string command)
	{
		command = command.Trim();
		string[] splitCommand = command.Split();
        if (Regex.IsMatch(command, @"^(toggle|press)\s+outer(\s+[1-9])+$",RegexOptions.IgnoreCase))
        {
			yield return null;
			foreach(int i in splitCommand.Skip(2).Select(x => int.Parse(x) - 1))
            {
				oS[i].selectable.OnInteract();
				yield return new WaitForSeconds(.1f);
            }
        }
		else if (Regex.IsMatch(command, @"^(toggle|press)\s+inner(\s+[1-3])+$",RegexOptions.IgnoreCase))
        {
			yield return null;
			foreach (int i in splitCommand.Skip(2).Select(x => int.Parse(x) - 1))
			{
				innerStands[i].selectable.OnInteract();
				yield return new WaitForSeconds(.1f);
			}
		}
		else if (Regex.IsMatch(command, @"^press\s+center\s*(\seven|\sodd)?$",RegexOptions.IgnoreCase))
        {
			yield return null;
            if (splitCommand.Length != 2)
				yield return new WaitUntil(() => (((int)(Get<KMBombInfo>().GetTime())) % 2 == 0) == (splitCommand.Last().EqualsIgnoreCase("even")) && Get<KMBombInfo>().GetTime() % 1 >= .2);		
			centerStand.selectable.OnInteract();
		}
	}

	private IEnumerator TwitchHandleForcedSolve()
	{
        while (!IsSolved)
        {
			switch (stageNumber)
			{
				case 0:
					yield return new WaitUntil(() => (((int)(Get<KMBombInfo>().GetTime())) % 2 == 0) == expectedValues[0] && Get<KMBombInfo>().GetTime()%1>=.2);
					centerStand.selectable.OnInteract();
					break;
				case 1:
					for(int i=0;i<3;i++)
						if(innerValues[i]!=expectedValues[i])
                        {
							innerStands[i].selectable.OnInteract();
							yield return new WaitForSeconds(.1f);
                        }
					centerStand.selectable.OnInteract();
					break;
				case 2:
					for(int i = 0; i < 9; i++)
                    {
                        if (outerValues[i / 3, i % 3] != expectedValues[i])
                        {
							outerStands[i / 3][i % 3].selectable.OnInteract();
							yield return new WaitForSeconds(.1f);
                        }
                    }
					centerStand.selectable.OnInteract();
					break;
			}
			yield return new WaitForSeconds(.05f);
        }	
	}
}
