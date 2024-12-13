# Bot Anty-Telemarketer

## Aby go uruchomić potrzebujesz:
* Płatne konto w Twilio.com . Polski numer telefonu (tylko komórkowe mają) kosztuje 3 USD miesięcznie. Wymaga to przesłania do Twilio zdjęć polskiego dokumentu tożsamości. Bez potwierdzenia tego można dostać numer telefonu w Finlandi, za 1.5 USD miesięcznie, od ręki.
* Azure Function : https://docs.microsoft.com/en-us/azure/azure-functions/create-first-function-vs-code-csharp?tabs=in-process )
** TYLKO jeśli ten "program" ma być uruchomiony w chmurze
* ~~Azure Cognitive Services (Free tier powinno dać radę) : https://docs.microsoft.com/en-us/azure/cognitive-services/speech-service/quickstarts/setup-platform?pivots=programming-language-csharp&tabs=dotnetcore%2Cwindows%2Cjre%2Cbrowser~~ w niedalekiej przyszłości to się przyda
* Stworzyć apkę na https://apps.dev.microsoft.com i włączyć Public Client FLows w sekcji Authentications
* Opcjonalnie (jeśli chcesz mieć powiadomienia) konto w PushOver i aplikacja na telefonie

## Konfiguracja
W przypadku lokalnego uruchomienia, skopiuj plik `local.settings.json.example' w to samo miejsce jako 'local.settings.json' i wpisz parametry wedle opisu poniżej.
W przypadku wdrożenia w Azure Functions, dodaj te wpisy w sekcji Configuration / Application Settings

* TokenCacheStorageContainer - nazwa kontera w Azure Storage, w którym będzie przechowywany cache tokena, dający dostęp do Onedrive
* ~~SpeechKey - klucz dostępowy do Azure Speech Service~~
* ~~SpeachLocationRegion - lokalizacja/region , np westeurope~~
* ~~SpeechRecognitionLanguage - język komunikacji .~~
* OnedriveApplicationCliendId - Application / client ID po stworzeniu jej na apps.dev.microsoft.com
* OnedriveFolderName - nazwa folderu na Onedrive (osobistym), w którym mają się znaleźc nagrania rozmów. Folder zostanie stworzony jeśli go nie ma
* PushOverUserId - dane z PushOver , jeśli chcesz mieć powiadomienia o nowym nagraniu lub wygaśnięciu tokena dostępowego do Onedrive
* PushOverAppTokenId - dane z PushOver , jeśli chcesz mieć powiadomienia o nowym nagraniu lub wygaśnięciu tokena dostępowego do Onedrive
* TwilioAuthToken - Twój osobisty token dostepowy z Twilio
* ConversationTemplateFileName - nazwa pliku szablonami wypowiedzi/odpowiedz
* PartialSpeechRecognitionEnabled - nie czekamy na koniec wypowiedzi telemarketera, tylko szukamy słów kluczowych wcześniej
* PartialSpeechMinimumChars - ile znaków musi być aby zrobić częsciowe rozpoznanie mowy
* SaySomethingWhenPartialSpeechLongerThanChars - gdy wypowiedź jest za długa to potrzymaj rozmowe
* KeepConversationKeyWord - który zestaw wypowiedzi używaj do podtrzymania rozmowy

Może też przydać się ngrok (https://ngrok.com/) . Łatwa i darmowa metoda na bezpieczny tuner dający dostęp z świata w czasie uruchomienia ngroka, na jeden port na naszym komputerze .

Do tego wszystkiego .NET SDK 8.

12go grudnia 2024 projekt został zreanimowany / zmigrowany z AzureFunctions v3 do v4 oraz z .net3 do .net8.
Development rozwiazania, w moim przypadku, opiera się do Visual Studio Code z projektem w kontenerze: https://code.visualstudio.com/docs/devcontainers/containers . Obecnie pliki zakładają development na platfromie arm64 ; w przypadku innej architektury sprawdź pliki w katalogu .devcontainer i, stosowanie, popraw architekturę na amd64.

Powodzenia!
Mile widziane PR.
