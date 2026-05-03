Feature: Shopping Lists

  Background:
    Given I am logged in with an active household

  Scenario: User creates a shopping list
    When I navigate to "/lists/create"
    And I fill in the list name "Weekly Groceries"
    And I submit the list form
    Then I am on the list view page

  Scenario: Created list appears on lists overview
    Given there is a list named "Weekly Groceries"
    When I navigate to "/lists"
    Then "Weekly Groceries" appears in the list overview

  Scenario: User adds an item to a list
    Given there is a list named "Weekly Groceries"
    When I open the list "Weekly Groceries"
    And I add item "Milk" to the list
    Then "Milk" appears in the list

  Scenario: User checks off a list item
    Given there is a list named "Weekly Groceries" with item "Milk"
    When I open the list "Weekly Groceries"
    And I toggle "Milk" as done
    Then "Milk" is shown as checked

  Scenario: User deletes a list
    Given there is a list named "Old List"
    When I navigate to "/lists"
    And I delete the list "Old List"
    Then "Old List" no longer appears in the list overview
