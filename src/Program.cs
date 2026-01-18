using Newtonsoft.Json;
using System.Diagnostics;
using System.Text;
using Terminal.Gui.App;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

using IApplication app = Application.Create();
app.Init();

var window = new Window {
	Title = "Symptom Tracker (Esc to quit)",
};

// Setup
var configPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "symptom-tracker");
var dataFileName = "data.json";
var notesPath = Path.Combine(configPath, "notes");
var dataPath = Path.Combine(configPath, dataFileName);
if (!Directory.Exists(configPath))
	Directory.CreateDirectory(configPath);
if (!File.Exists(dataPath))
	File.Copy(dataFileName, dataPath);
if (!Directory.Exists(notesPath))
	Directory.CreateDirectory(notesPath);

// Read database
var data = JsonConvert.DeserializeObject<Data>(File.ReadAllText(dataPath));
if (data is null)
	throw new Exception("Error processing data file");
if (data.Symptoms.Any(x => x.Weight > 1 || x.Weight <= 0))
	throw new Exception("Symptom weights must be between 0 and 1");
var symptomMap = new Dictionary<string, Symptom>();
foreach(var s in data.Symptoms) {
	symptomMap.Add(s.Id, s);
}

//UI Setup
var date = DateTime.Today.ToString("yyyy-MM-dd");
var mainElements = new List<View>();
var childElements = new List<View>();

var lDate = new Label() {
	Text = $"Hello {data.Name}, today's date is {date}. What would you like to do?\n",
	Y = 1,
};
mainElements.Add(lDate);

var bTrack = createMainButton("Track Symptoms", 3);
var bTodaysReport = createMainButton("See Today's Report", 4);
var bNote = createMainButton("Edit Daily Note", 5);
var bPreviousDays = createMainButton("View Previous Days", 6);
var bSymptoms = createMainButton("My Symptoms", 7);
var bActions = createMainButton("My Suggested Actions", 8);

bTodaysReport.Accepting += (s, e) => {
	clearMainScreen();
	var today = DateOnly.FromDateTime(DateTime.Today);
 	generateReport(today);
	e.Handled = true;
};

bNote.Accepting += (s, e) => {
	clearMainScreen();
	var notePath = Path.Combine(notesPath, $"{date}.md");
	var noteText = string.Empty;
	if (!File.Exists(notePath))
		File.Create(notePath);
	else
		noteText= File.ReadAllText(notePath);
	var editor = new TextView {
		Text = noteText,
		X = 0,
		Y = 0,
		Width = Dim.Fill(),
		Height = Dim.Fill() - 2,
	};
	childElements.Add(editor);
	var saveButton = new Button {
		Text = "Save",
		Y = Pos.Bottom(editor),
	};
	saveButton.Accepting += (s, e) => {
		File.WriteAllText(notePath, editor.Text);
		e.Handled = true;
	};
	childElements.Add(saveButton);
	var backButton = createBackButton();
	backButton.Y = Pos.Bottom(editor);
	backButton.X = Pos.Right(saveButton);
	showChildElements();
	e.Handled = true;
};

showMainScreen();

app.Run(window);

// // Initial user prompt
//
// switch(answerNumber) {
// 	case 4:
// 		Console.WriteLine($"Please enter the day (eg {date})");
// 		var a = Console.ReadLine();
// 		if (a is null)
// 			throw new Exception("Error, please provide a date string");
// 		var d = DateOnly.Parse(a);
// 		generateReport(d);
// 		break;
// }

void showElements(List<View> elements) {
	foreach (var e in elements)
		window.Add(e);
}

void clearElements(List<View> elements) {
	foreach (var e in elements)
		window.Remove(e);
}

void showMainScreen() {
	showElements(mainElements);
}

void clearMainScreen() {
	clearElements(mainElements);
}

void showChildElements() {
	showElements(childElements);
}

void clearChildElements() {
	clearElements(childElements);
	childElements.Clear();
}

Button createMainButton(string text, int y) {
	var b = new Button() {
		Text = text,
		Y = y,
		HotKeySpecifier = (Rune)0xffff,
	};
	mainElements.Add(b);
	return b;
}

Button createBackButton() {
	var backButton = new Button {
		Text = "Back",
	};
	childElements.Add(backButton);
	backButton.Accepting += (s, e) => {
		clearChildElements();
		showMainScreen();
		e.Handled = true;
	};
	return backButton;
}

void generateReport(DateOnly d) {
	if (data.Days is null)
		throw new Exception("You don't have any days recorded.");
	var day = data.Days.FirstOrDefault(x => x.Date == d);
	if (day is null || day.TrackedSymptoms is null || !day.TrackedSymptoms.Any())
		throw new Exception("No submission found for this day");
	childElements.Add(new Label {
		Title = "Here are your symptom submissions.",
		Y = 1,
	});
	double total = 0;
	double weightSum = 0;
	var i = 0;
	for (i = 0; i < day.TrackedSymptoms.Count; i++) {
		var t = day.TrackedSymptoms[i];
		var s = symptomMap[t.Id];
		childElements.Add(new Label {
			Title = $"{s.Name}: {t.Value}",
			Y = i + 3,
		});
		double v = t.Value;
		if (!s.HigherBetter)
			v = 10 - v;
		v *= s.Weight;
		total += v;
		weightSum += s.Weight;
	}
	total /= weightSum;
	total = Math.Round(total, 1);
	childElements.Add(new Label {
		Title = $"Your overall score is {total}",
		Y = i + 4,
	});
	var backButton = createBackButton();
	backButton.Y = i + 6;
	showChildElements();
}

class Data {
	required public string Name { get; set; }
	required public List<Symptom> Symptoms { get; set; }
	public List<Day>? Days { get; set; }
	required public List<Action> Actions { get; set; }
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

class Action {
	required public string Name { get; set; }
	required public List<int> Scores { get; set; }
}

