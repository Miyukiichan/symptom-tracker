using Newtonsoft.Json;
using System.Diagnostics;

// Setup
var configPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "symptom-tracker");
var dataFileName = "data.json";
var notesPath = Path.Combine(configPath, "notes");
var dataPath = Path.Combine(configPath, dataFileName);
if (!Directory.Exists(configPath)) {
	Console.WriteLine("Would you like to go through first time setup? (y/n)");
	var a = Console.ReadLine();
	if (a is null || a.ToLower() != "y")
		return;
	Directory.CreateDirectory(configPath);
	File.Copy(dataFileName, dataPath);
}
if (!Directory.Exists(notesPath))
	Directory.CreateDirectory(notesPath);

// Read database
var date = DateTime.Today.ToString("yyyy-MM-dd");
var data = JsonConvert.DeserializeObject<Data>(File.ReadAllText(dataPath));
if (data is null)
	throw new Exception("Error processing data file");
if (data.Symptoms.Any(x => x.Weight > 1 || x.Weight <= 0))
	throw new Exception("Symptom weights must be between 0 and 1");
var symptomMap = new Dictionary<string, Symptom>();
foreach(var s in data.Symptoms) {
	symptomMap.Add(s.Id, s);
}

// Initial user prompt
Console.WriteLine($"Hello {data.Name}, today's date is {date}. What would you like to do?\n");
Console.WriteLine($"1. Track my symptoms");
Console.WriteLine($"2. See today's report");
Console.WriteLine($"3. Edit daily note");
Console.WriteLine($"4. See report from a previous day");
var answer = string.Empty;
var answerNumber = -1;
while (string.IsNullOrWhiteSpace(answer)) {
	answer = Console.ReadLine();
	if (!int.TryParse(answer, out answerNumber)) {
		Console.WriteLine("Please input the option number only.");
		answer = string.Empty;
		continue;
	}
	if (answerNumber != 1 && answerNumber != 2 && answerNumber != 3 && answerNumber != 4) {
		Console.WriteLine("Invalid option.");
		answer = string.Empty;
		continue;
	}
}
switch(answerNumber) {
	case 2:
		if (data.Days is null)
			throw new Exception("You don't have any days recorded.");
		var today = data.Days.FirstOrDefault(x => x.Date == DateOnly.FromDateTime(DateTime.Today));
		if (today is null || today.TrackedSymptoms is null || !today.TrackedSymptoms.Any())
			throw new Exception("No submission found for today");
		Console.WriteLine("Here are your symptom submissions for today.");
		double total = 0;
		double weightSum = 0;
		foreach(var t in today.TrackedSymptoms) {
			var s = symptomMap[t.Id];
			Console.WriteLine($"{s.Name}: {t.Value}");
			double v = t.Value;
			if (s.HigherBetter)
				v = 10 - v;
			v *= s.Weight;
			total += v;
			weightSum += s.Weight;
		}
		total /= weightSum;
		total = Math.Round(total, 1);
		Console.WriteLine($"Today, your overall score is {total}");
		break;
	case 3: 
		var notePath = Path.Combine(notesPath, $"{date}.md");
		if (!File.Exists(notePath))
			File.Create(notePath);
		new Process {
			StartInfo = new ProcessStartInfo(notePath) {
				UseShellExecute = true,
			}
		}.Start();
		break;
}

class Data {
	required public string Name { get; set; }
	required public List<Symptom> Symptoms { get; set; }
	public List<Day>? Days { get; set; }
}

class Symptom {
	required public string Id { get; set; }
	required public string Name { get; set; }
	public bool HigherBetter { get; set; } = false;
	public double Weight { get; set; } = 1;
}

class Day {
	required public DateOnly Date { get; set; }
	public List<TrackedSymptom>? TrackedSymptoms { get; set; }
}

class TrackedSymptom {
	required public string Id { get; set; }
	required public int Value { get; set; }
}
