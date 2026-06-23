Feature: Recipes

  Background:
    Given I am logged in with an active household

  Scenario: User creates a recipe
    When I navigate to "/recipes/create"
    And I fill in the recipe name "Pasta Carbonara"
    And I submit the recipe form
    Then I am on the recipe edit page for "Pasta Carbonara"

  Scenario: User adds an ingredient to a recipe
    Given there is a recipe named "Pasta Carbonara"
    When I open the recipe "Pasta Carbonara" for editing
    And I add ingredient "Eggs" to the recipe
    Then "Eggs" appears in the recipe items

  Scenario: User adds a quantity to a recipe ingredient via edit mode
    Given there is a recipe named "Pasta Carbonara"
    When I open the recipe "Pasta Carbonara" for editing
    And I add ingredient "Eggs" to the recipe
    And I open the ingredient item menu for "Eggs"
    And I start editing the item
    And I open the "quantity" composer panel
    And I set the quantity to "3"
    And I save the recipe item edit
    Then the recipe item "Eggs" shows quantity "3"

  Scenario: Recipe view is read-only and links to edit
    Given there is a recipe named "Pasta Carbonara"
    When I open the recipe "Pasta Carbonara"
    Then the recipe view is read-only
    When I tap the edit recipe button
    Then I am on the recipe edit page for "Pasta Carbonara"

  Scenario: Editing recipe metadata auto-saves
    Given there is a recipe named "Pasta Carbonara"
    When I open the recipe "Pasta Carbonara" for editing
    And I set the recipe description to "Quick weeknight dinner"
    And I open the recipe "Pasta Carbonara"
    Then the recipe description shows "Quick weeknight dinner"

  Scenario: Viewing a recipe scales ingredient quantities to the chosen servings
    Given there is a recipe named "Pancakes" with servings 4
    And the recipe "Pancakes" has an ingredient "Flour" with quantity 200 "Gram"
    When I open the recipe "Pancakes"
    And I increment the servings
    Then the recipe item "Flour" shows quantity "250"

  Scenario: User deletes a recipe from the recipe list
    Given there is a recipe named "Old Recipe"
    When I navigate to "/recipes"
    And I open the recipe card menu for "Old Recipe"
    And I confirm deleting the recipe "Old Recipe" from the card menu
    Then "Old Recipe" no longer appears in the recipe overview

  Scenario: Expanding a recipe card peeks its ingredients without leaving the overview
    Given there is a recipe named "Pancakes"
    And the recipe "Pancakes" has an item "Flour"
    When I navigate to "/recipes"
    And I expand the recipe card "Pancakes"
    Then the recipe card peek for "Pancakes" shows "Flour"
    When I open the peeked recipe "Pancakes"
    Then I am on the recipe view page for "Pancakes"
