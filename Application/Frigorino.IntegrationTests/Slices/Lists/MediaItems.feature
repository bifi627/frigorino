Feature: Media Items

  Background:
    Given I am logged in with an active household

  Scenario: User attaches a photo and views it
    Given there is a list named "Trip"
    When I open the list "Trip"
    And I attach a photo with caption "beach"
    Then a photo thumbnail appears in the list
    When I open the photo
    Then the image lightbox is shown

  Scenario: Photo thumbnail survives being checked off
    Given there is a list named "Trip"
    When I open the list "Trip"
    And I attach a photo with caption "beach"
    Then a photo thumbnail appears in the list
    When I check off the photo
    Then the photo thumbnail is still shown

  Scenario: User attaches a document and sees it in the list
    Given there is a list named "Trip"
    When I open the list "Trip"
    And I attach a document to the list with caption "warranty"
    Then a document row appears in the list
