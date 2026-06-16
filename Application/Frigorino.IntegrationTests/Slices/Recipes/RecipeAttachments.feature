Feature: Recipe attachments

  Background:
    Given I am logged in with an active household

  Scenario: User attaches an image to a recipe and views it in the lightbox
    Given there is a recipe named "Pasta Carbonara"
    When I open the recipe "Pasta Carbonara" for editing
    And I expand the attachments section
    And I attach an image with caption "Finished dish"
    Then the attachments list shows an attachment captioned "Finished dish"
    When I open the recipe "Pasta Carbonara"
    And I open the attachment tile captioned "Finished dish"
    Then the attachment lightbox shows the full-resolution image

  Scenario: User attaches a PDF document to a recipe
    Given there is a recipe named "Pasta Carbonara"
    When I open the recipe "Pasta Carbonara" for editing
    And I expand the attachments section
    And I attach a document with caption "Recipe sheet"
    Then the attachments list shows an attachment captioned "Recipe sheet"
    When I open the recipe "Pasta Carbonara"
    Then the attachment tile captioned "Recipe sheet" is shown

  Scenario: User edits an attachment caption
    Given there is a recipe named "Pasta Carbonara"
    And the recipe "Pasta Carbonara" has an image attachment captioned "Old caption"
    When I open the recipe "Pasta Carbonara" for editing
    And I expand the attachments section
    And I edit the attachment captioned "Old caption" to "New caption"
    Then the attachments list shows an attachment captioned "New caption"

  Scenario: User reorders attachments by dragging
    Given there is a recipe named "Pasta Carbonara"
    And the recipe "Pasta Carbonara" has an image attachment captioned "First"
    And the recipe "Pasta Carbonara" has an image attachment captioned "Second"
    When I open the recipe "Pasta Carbonara" for editing
    And I expand the attachments section
    And I drag the attachment captioned "Second" above "First"
    Then the first attachment is captioned "Second"

  Scenario: User deletes an attachment
    Given there is a recipe named "Pasta Carbonara"
    And the recipe "Pasta Carbonara" has an image attachment captioned "To delete"
    When I open the recipe "Pasta Carbonara" for editing
    And I expand the attachments section
    And I delete the attachment captioned "To delete"
    Then the attachment captioned "To delete" is no longer listed
