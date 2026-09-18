// See https://aka.ms/new-console-template for more information
// Console.WriteLine("Hello, World!");
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using IssueTracker.Core.Entities;
using IssueTracker.Core.Enums;

var project = new Project("Project 1");
var projectId = project.Id;
var items = new List<WorkItem>{
    new WorkItem(projectId, "Create a project"),
    new WorkItem(projectId, "Add issue"),
    new WorkItem(projectId, "Edit issue"),
    new WorkItem(projectId, "Add comment"),
    new WorkItem(projectId, "Close issue")
};
items[0].ChangeStatus(WorkItemStatus.InProgress);
items[1].ChangeStatus(WorkItemStatus.InProgress);
items[0].ChangeStatus(WorkItemStatus.Done);

foreach (var item in items)
{
    Console.WriteLine($"{item.Title}: {item.Status}");
}

var todoItems = items.Where(item => item.Status != WorkItemStatus.Done).ToList();

var counts = todoItems
    .GroupBy(item => item.Status)
    .Select(group => new
    {
        Status = group.Key,
        Count = group.Count()
    });

foreach (var count in counts)
{
    Console.WriteLine($"{count.Status}: {count.Count}");
}

var idToFind = items[0].Id;
var foundItem = items.FirstOrDefault(x => x.Id == idToFind);

if (foundItem is not null)
{
    foundItem.Rename("Renamed");
    Console.WriteLine($"Rename: {foundItem.Title}");
}
else
{
    Console.WriteLine("Unknown ID");
}

// items[2].ChangeStatus(WorkItemStatus.Done);
while (true)
{
    Console.WriteLine("\n1. List items");
    Console.WriteLine("\n2. Rename an item");
    Console.WriteLine("\n0. Exit");

    var choice = Console.ReadLine();

    if (choice is null || choice == "0")
        break;

    switch (choice)
    {
        case "1":
            foreach (var item in items)
                Console.WriteLine($"{item.Id} | {item.Title} | {item.Status}");
            break;

        case "2":
            // Your next task: read an ID, find the item, then rename it.
            var input = Console.ReadLine();
            if (Guid.TryParse(input, out Guid inputId))
            {
                var input_foundItem = items.FirstOrDefault(x => x.Id == inputId);
                if (input_foundItem == null)
                {
                    System.Console.WriteLine("Unknown Id");
                }
                else
                {
                    Console.WriteLine("What's new name");
                    string? newName = Console.ReadLine();
                    if (string.IsNullOrWhiteSpace(newName))
                    {
                        Console.WriteLine("Please enter a title.");
                    }
                    else
                    {
                        try
                        {
                            input_foundItem.Rename(newName);
                        }
                        catch (ArgumentException ex)
                        {
                            Console.WriteLine(ex.Message);
                        }
                    }
                }

            }
            else
            {
                Console.WriteLine("Invalid Id");
            }

            break;

        default:
            Console.WriteLine("Unknown option.");
            break;
    }
}
