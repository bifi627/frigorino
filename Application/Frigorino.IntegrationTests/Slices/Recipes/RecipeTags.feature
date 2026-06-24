Feature: Recipe tags
  Recipes can be tagged with curated course/dietary tags, filtered on the overview,
  and offered AI tag suggestions on demand.

  Background:
    Given I am logged in with an active household

  Scenario: Tags set on a recipe persist and show on the view
    Given there is a recipe named "Margherita Pizza"
    And the recipe "Margherita Pizza" has tags "Main,Vegetarian"
    When I open the recipe "Margherita Pizza"
    Then the recipe view shows the tag "Main"
    And the recipe view shows the tag "Vegetarian"

  Scenario: Overview tag filter narrows the list
    Given there is a recipe named "Chicken Curry"
    And the recipe "Chicken Curry" has tags "Main"
    And there is a recipe named "Fruit Salad"
    And the recipe "Fruit Salad" has tags "Salad"
    When I open the recipes overview
    And I toggle the overview tag filter "Salad"
    Then the recipe "Fruit Salad" appears in the recipe overview
    And "Chicken Curry" no longer appears in the recipe overview

  Scenario: Suggest tags offers ghost chips the user can accept
    Given there is a recipe named "Carrot Cake"
    When I open the recipe "Carrot Cake" for editing
    And I tap suggest tags
    Then a suggested tag chip "Dessert" is shown
    When I accept the suggested tag "Dessert"
    Then the recipe tag "Dessert" is selected
