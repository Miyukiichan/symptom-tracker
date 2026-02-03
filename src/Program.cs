using Newtonsoft.Json;
using System.Diagnostics;
using System.Text;
using Terminal.Gui.App;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

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

var window = new Window {
	Title = "Symptom Tracker (Esc to quit)",
};
var pageHistory = new Stack<List<View>>();
var today = DateOnly.FromDateTime(DateTime.Today);
var date = DateTime.Today.ToString("dddd dd/MM/yyyy");
var todayName = DateTime.Today.ToString("dddd");
var offDayToday = data.OffDays.Contains(todayName);
var onOff = offDayToday ? "OFF" : "ON";
var mainElements = new List<View>();

var lDate = new Label() {
	Text = $"Hello {data.Name}, today is {date} and it is an {onOff} day. What would you like to do?\n",
	Y = 1,
};
mainElements.Add(lDate);

var bTrack = createMainButton("Track Symptoms", 3);
var bTodaysReport = createMainButton("See Today's Report", 4);
var bNote = createMainButton("Edit Daily Note", 5);
var bPreviousDays = createMainButton("View Previous Days", 6);
var bSymptoms = createMainButton("My Symptoms", 7);
var bActions = createMainButton("My Suggested Actions", 8);

bTrack.Accepting += (s, e) => {
	var elements = new List<View>();
	var labelWidth = data.Symptoms.Select(x => x.Name.Count()).Max() + 1;
	var selectorMap = new Dictionary<string, OptionSelector>();
	if (data.Days is null)
		data.Days = new List<Day>();
	var day = data.Days.FirstOrDefault(x => x.Date == today);
	if (day is null) {
		day = new Day {
			Date = today,
		};
		data.Days.Add(day);
	}
	if (day.TrackedSymptoms is null)
		day.TrackedSymptoms = new List<TrackedSymptom>();
	data.Symptoms = data.Symptoms.OrderBy(x => x.Name).ToList();
	OptionSelector? firstSelector = null;
	for (var i = 0; i < data.Symptoms.Count; i++) {
		var symptom = data.Symptoms[i];
		var initialValue = 0;
		if (day.TrackedSymptoms.Any()) {
			var t = day.TrackedSymptoms.FirstOrDefault(x => x.Id == symptom.Id);
			if (t is not null)
				initialValue = t.Value + 1;
		}
		var label = new Label {
			Text = symptom.Name,
			Y = i,
		};
		elements.Add(label);
		var selector = new OptionSelector {
			Labels = new List<string> {"N/A", "0", "1", "2", "3", "4", "5", "6", "7", "8", "9", "10"},
			Y = i,
			X = labelWidth,
			Orientation = Orientation.Horizontal,
			Value = initialValue,
		};
		selectorMap.Add(symptom.Id, selector);
		elements.Add(selector);
		if (i == 0)
			firstSelector = selector;
	}
	var submitButton = createButton("Submit", elements);
	submitButton.Y = data.Symptoms.Count + 1;
	submitButton.Accepting += (s, e) => {
		foreach (var entry in selectorMap) {
			var tracked = day.TrackedSymptoms.FirstOrDefault(x => x.Id == entry.Key);
			var value = entry.Value.Value.GetValueOrDefault();
			// Treat 0 as null entry
			if (value == 0) {
				if (tracked is not null)
					day.TrackedSymptoms.Remove(tracked);
				continue;
			}
			// Convert to true numerical score offset by 1
			value -= 1;
			if (tracked is null) {
				tracked = new TrackedSymptom {
					Id = entry.Key,
					Value = value,
				};
				day.TrackedSymptoms.Add(tracked);
			}
			else {
				tracked.Value = value;
			}
		}
		File.WriteAllText(dataPath, JsonConvert.SerializeObject(data, Formatting.Indented));
		var reportElements = new List<View>();
		generateReport(today, reportElements, bTrack);
		//Ensure we don't return to the tracking screen - just go back to main menu
		hideElements(pageHistory.Pop());
		navigateForward(reportElements);
		e.Handled = true;
	};

	var backButton = createBackButton(elements, bTrack);
	backButton.Y = data.Symptoms.Count + 1;
	backButton.X = Pos.Right(submitButton);
	navigateForward(elements, firstSelector);
	e.Handled = true;
};

bTodaysReport.Accepting += (s, e) => {
	var elements = new List<View>();
 	generateReport(today, elements, bTodaysReport);
	navigateForward(elements);
	e.Handled = true;
};

bNote.Accepting += (s, e) => {
	showNote(today, bNote);
	e.Handled = true;
};

bPreviousDays.Accepting += (s, e) => {
	if (data.Days is null || !data.Days.Any())
		return;
	var elements = new List<View>();
	var backButton = createBackButton(elements, bPreviousDays);
	var table = new TableView() {
		Width = Dim.Fill(),
		Height = Dim.Fill() - 2,
		Y = 1,
	};
	table.Table = new EnumerableTableSource<Day>(data.Days.OrderByDescending(x => x.Date),
		new Dictionary<string, Func<Day, object>>() {
			{ "Date", (d) => d.Date.ToString("ddd dd/MM/yyyy") },
			{ "Score", (d) => calculateScore(d.Date) },
		}
	);
	table.CellActivated += (s, e) => {
		var index = e.Row;
		var d = table.Table[index, 0].ToString();
		if (d is null)
			return;
		var dOnly = DateOnly.Parse(d);
		var reportElements = new List<View>();
		generateReport(dOnly, reportElements, table);
		navigateForward(reportElements);
	};
	elements.Add(table);
	navigateForward(elements, table);
	e.Handled = true;
};

navigateForward(mainElements);
using IApplication app = Application.Create();
app.Init();
app.Run(window);

void navigateBack(View? elementToFocus = null) {
	if (!pageHistory.Any())
		return;
	hideElements(pageHistory.Pop());
	showElements(pageHistory.Peek(), elementToFocus);
}

void navigateForward(List<View> elements, View? elementToFocus = null) {
	if (pageHistory.Any())
		hideElements(pageHistory.Peek());
	pageHistory.Push(elements);
	showElements(elements, elementToFocus);
}

void showElements(List<View> elements, View? elementToFocus = null) {
	foreach (var e in elements)
		window.Add(e);
	if (elementToFocus is not null)
		elementToFocus.SetFocus();
}

void hideElements(List<View> elements) {
	foreach (var e in elements)
		window.Remove(e);
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

Button createButton(string text, List<View>? elements = null) {
	var b = new Button {
		Text = text,
		HotKeySpecifier = (Rune)0xffff,
	};
	if (elements is not null)
		elements.Add(b);
	return b;
}

Button createBackButton(List<View>? elements = null, View? elementToFocus = null) {
	var backButton = new Button {
		Text = "Back",
		HotKeySpecifier = (Rune)0xffff,
	};
	if (elements is not null)
		elements.Add(backButton);
	backButton.Accepting += (s, e) => {
		navigateBack(elementToFocus);
		e.Handled = true;
	};
	return backButton;
}

double calculateScore(DateOnly d) {
	var day = data.Days.FirstOrDefault(x => x.Date == d);
	if (day is null || day.TrackedSymptoms is null || !day.TrackedSymptoms.Any())
		throw new Exception("No submission found for this day");
	double total = 0;
	double weightSum = 0;
	for (var i = 0; i < day.TrackedSymptoms.Count; i++) {
		var t = day.TrackedSymptoms[i];
		var s = symptomMap[t.Id];
		double v = t.Value;
		if (!s.HigherBetter)
			v = 10 - v;
		v *= s.Weight;
		total += v;
		weightSum += s.Weight;
	}
	total /= weightSum;
	total = Math.Round(total, 1);
	return total;
}

void generateReport(DateOnly d, List<View> elements, View? backElementToFocus = null) {
	if (data.Days is null)
		throw new Exception("You don't have any days recorded.");
	var day = data.Days.FirstOrDefault(x => x.Date == d);
	if (day is null || day.TrackedSymptoms is null || !day.TrackedSymptoms.Any())
		throw new Exception("No submission found for this day");
	day.TrackedSymptoms = day.TrackedSymptoms.OrderBy(x => symptomMap[x.Id].Name).ToList();
	elements.Add(new Label {
		Title = "Here are your symptom submissions.",
		Y = 1,
	});
	for (var i = 0; i < day.TrackedSymptoms.Count; i++) {
		var t = day.TrackedSymptoms[i];
		var s = symptomMap[t.Id];
		elements.Add(new Label {
			Title = $"{s.Name}: {t.Value}",
			Y = i + 3,
		});
	}
	var total = calculateScore(d);
	var count = day.TrackedSymptoms.Count;
	elements.Add(new Label {
		Title = $"Your overall score is {total}",
		Y = count + 4,
	});
	var noteButton = createButton("Edit Note", elements);
	noteButton.Y = count + 6;
	noteButton.Accepting += (s, e) => {
		showNote(d, noteButton);
		e.Handled = true;
	};
	var backButton = createBackButton(elements, backElementToFocus);
	backButton.Y = count + 6;
	backButton.X = Pos.Right(noteButton);
}

void showNote(DateOnly d, View? backElementToFocus = null) {
	var elements = new List<View>();
	var notePath = Path.Combine(notesPath, $"{d.ToString("yyyy-MM-dd")}.md");
	var noteText = string.Empty;
	Console.WriteLine(notePath);
	if (!File.Exists(notePath))
		File.Create(notePath);
	else
		noteText= File.ReadAllText(notePath);
	var editor = new TextView {
		Text = noteText,
		Width = Dim.Fill(),
		Height = Dim.Fill() - 2,
	};
	elements.Add(editor);
	var saveButton = createButton("Save", elements);
	saveButton.Y = Pos.Bottom(editor);
	saveButton.Accepting += (s, e) => {
		File.WriteAllText(notePath, editor.Text);
		e.Handled = true;
	};
	var backButton = createBackButton(elements, backElementToFocus);
	backButton.Y = Pos.Bottom(editor);
	backButton.X = Pos.Right(saveButton);
	navigateForward(elements, editor);
}

class Data {
	required public string Name { get; set; }
	required public List<Symptom> Symptoms { get; set; }
	public List<Day>? Days { get; set; }
	required public List<Action> Actions { get; set; }
	required public List<string> OffDays { get; set; }
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
	required public int MinScore { get; set; }
	required public int MaxScore { get; set; }
}

