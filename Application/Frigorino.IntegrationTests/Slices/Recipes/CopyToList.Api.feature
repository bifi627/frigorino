Feature: Copy recipe to shopping list (API)

  Background:
    Given I am logged in with an active household

  Scenario: Copying selected ingredients adds them to the target list
    Given there is a recipe named "Pancakes"
    And the recipe "Pancakes" has an item "Flour" with quantity 250 unit 0
    And the recipe "Pancakes" has an item "Milk" with quantity 300 unit 2
    And there is a list named "Groceries"
    When I copy items "Flour, Milk" from recipe "Pancakes" to list "Groceries" via the API
    Then the API response status is 200
    And the copy response reports 2 copied
    And the list "Groceries" contains an item "Flour" with quantity 250 unit 0
    And the list "Groceries" contains an item "Milk" with quantity 300 unit 2
    And every item in list "Groceries" is unchecked

  Scenario: Deselected ingredients are not copied
    Given there is a recipe named "Pancakes"
    And the recipe "Pancakes" has an item "Flour" with quantity 250 unit 0
    And the recipe "Pancakes" has an item "Milk" with quantity 300 unit 2
    And there is a list named "Groceries"
    When I copy items "Flour" from recipe "Pancakes" to list "Groceries" via the API
    Then the API response status is 200
    And the copy response reports 1 copied
    And the list "Groceries" does not contain an item "Milk"

  Scenario: A text-only ingredient is copied with no quantity
    Given there is a recipe named "Pancakes"
    And the recipe "Pancakes" has an item "Salt to taste"
    And there is a list named "Groceries"
    When I copy text-only item "Salt to taste" from recipe "Pancakes" to list "Groceries" via the API
    Then the API response status is 200
    And the copy response reports 1 copied
    And the list "Groceries" contains a text-only item "Salt to taste"

  Scenario: A stale recipe-item id is skipped, not an error
    Given there is a recipe named "Pancakes"
    And the recipe "Pancakes" has an item "Flour" with quantity 250 unit 0
    And there is a list named "Groceries"
    When I copy items "Flour" plus a stale id from recipe "Pancakes" to list "Groceries" via the API
    Then the API response status is 200
    And the copy response reports 1 copied

  Scenario: Copying to a non-existent list returns 404
    Given there is a recipe named "Pancakes"
    And the recipe "Pancakes" has an item "Flour" with quantity 250 unit 0
    When I copy item "Flour" from recipe "Pancakes" to a non-existent list via the API
    Then the API response status is 404

  Scenario: An empty item list is rejected
    Given there is a recipe named "Pancakes"
    And there is a list named "Groceries"
    When I copy no items from recipe "Pancakes" to list "Groceries" via the API
    Then the API response status is 400

  Scenario: A non-member cannot copy from a household's recipe
    Given I am logged in as "alice"
    And an existing household "Other" owned by "bob" that I am not a member of
    And "bob" has created a recipe named "BobsRecipe"
    When I attempt to copy from recipe "BobsRecipe" to a list in that household via the API
    Then the API response status is 404
