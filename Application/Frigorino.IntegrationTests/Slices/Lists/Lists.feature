Feature: Lists

  Background:
    Given I am logged in with an active household

  Scenario: User creates a shopping list
    When I navigate to "/lists/create"
    And I fill in the list name "Weekly Groceries"
    And I submit the list form
    Then I am on the list view page

  Scenario: User deletes a list
    Given there is a list named "Old List"
    When I navigate to "/lists"
    And I delete the list "Old List"
    Then "Old List" no longer appears in the list overview

  Scenario: User renames a list
    Given there is a list named "Old Name"
    When I open the list edit page for "Old Name"
    And I fill in the list name "New Name"
    And I save the list
    And I navigate to "/lists"
    Then "New Name" appears in the list overview
