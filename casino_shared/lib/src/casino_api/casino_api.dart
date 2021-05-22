import 'dart:convert';

import 'package:freezed_annotation/freezed_annotation.dart';
import 'package:http/http.dart';
import 'package:logger/logger.dart';

import '../uri_extensions.dart';

part 'casino_api.freezed.dart';
part 'casino_api.g.dart';

@freezed
class SlotMachineDTO with _$SlotMachineDTO {
  const factory SlotMachineDTO({
    required String id,
    required String name,
    required int tokens,
  }) = _SlotMachineDTO;

  factory SlotMachineDTO.fromJson(Map<String, dynamic> json) =>
      _$SlotMachineDTOFromJson(json);
}

@freezed
abstract class SlotMachine with _$SlotMachine {
  const factory SlotMachine({
    required String id,
    required String name,
    required int tokens,
  }) = _SlotMachine;

  @override
  String get id;
  @override
  String get name;
  @override
  int get tokens;
}

@freezed
class CasinoApiException with _$CasinoApiException {
  const factory CasinoApiException.notFound({
    required String message,
  }) = _NotFound;
  const factory CasinoApiException.invalidRequest({
    required String message,
  }) = _InvalidRequest;
  const factory CasinoApiException.clientError({
    required String message,
  }) = _ClientError;
  const factory CasinoApiException.serverError({
    required String message,
  }) = _ServerError;
}

class CasinoApiSettings {
  const CasinoApiSettings({required this.url});

  final Uri url;

  @override
  bool operator ==(Object other) {
    return other is CasinoApiSettings && other.url == url;
  }

  @override
  int get hashCode => super.hashCode ^ url.hashCode;
}

SlotMachine _convertDTOTOSlotMachine(SlotMachineDTO input) {
  return SlotMachine(id: input.id, name: input.name, tokens: input.tokens);
}

class CasinoApi {
  const CasinoApi({
    required this.http,
    required this.config,
    required this.logger,
    this.debug = false,
  });

  final Client http;
  final CasinoApiSettings config;
  final Logger logger;
  final bool debug;

  Uri get url {
    return config.url.addPathSegments(['casino']);
  }

  Future<String> addSlotMachine(String name) async {
    logger.v('add slot machine: $name');
    final response = await http.post(
      url.addPathSegments(['slot-machines']),
      body: jsonEncode({'name': name}),
    );
    await validateResponse(response);
    return response.body;
  }

  Future<List<SlotMachine>> listSlotMachines() async {
    // commented out to keep log clear
    // logger.v('list slot-machines');
    final response = await http.get(
      url.addPathSegments(['slot-machines']),
    );
    await validateResponse(response);
    final list = jsonDecode(response.body) as List;
    return list
        .cast<Map<String, dynamic>>()
        .map((json) => SlotMachineDTO.fromJson(json))
        .map(_convertDTOTOSlotMachine)
        .toList();
  }

  Future<SlotMachine> getSlotMachineByName(String name) async {
    logger.v('get slot-machine by name: $name');
    final response = await http.get(url.addPathSegments(
      ['slot-machines', 'by-name', name],
    ));
    await validateResponse(response);
    final json = jsonDecode(response.body) as Map<String, dynamic>;
    return _convertDTOTOSlotMachine(SlotMachineDTO.fromJson(json));
  }

  Future<int> getTokens(String id) async {
    logger.v('get tokens: $id');
    final response = await http.get(url.addPathSegments(
      ['slot-machines', id, 'tokens'],
    ));
    await validateResponse(response);
    return int.parse(response.body);
  }

  Future<void> setTokens(String id, int count) async {
    assert(count >= 0);
    logger.v('set tokens: $id $count');
    final response = await http.put(url.replace(
      pathSegments: [...url.pathSegments, 'slot-machines', id, 'tokens'],
      queryParameters: {'count': count.toString()},
    ));
    await validateResponse(response);
  }

  Future<void> setName(String id, String name) async {
    assert(name.isNotEmpty);
    logger.v('set name: $id $name');
    final response = await http.put(url.replace(
      pathSegments: [...url.pathSegments, 'slot-machines', id, 'name'],
      queryParameters: {'name': name},
    ));
    await validateResponse(response);
  }

  Future<void> removeSlotMachine(String id) async {
    logger.v('remove slot-machine: $id');
    final response = await http.delete(url.addPathSegments(
      ['slot-machines', id],
    ));
    await validateResponse(response);
  }

  Future<void> validateResponse(Response response) async {
    if (response.statusCode < 400) {
      return;
    }
    if (debug) {
      final message =
          StringBuffer('Error while sending request to casino api:');
      if (response.request != null) {
        message.write('\n\tRequest:'
            '\n\t-uri: ${response.request!.url.toString()}'
            '\n\t-method: ${response.request!.method}');
      }
      message.write('\n\tResponse:'
          '\n\t-statusCode: ${response.statusCode}'
          '\n\t-headers: ${response.headers}'
          '\n\t-body: ${response.body}');
      logger.e(message.toString(), response.body, StackTrace.current);
    }
    if (response.statusCode == 400) {
      throw CasinoApiException.invalidRequest(message: response.body);
    } else if (response.statusCode == 404) {
      throw CasinoApiException.notFound(message: response.body);
    } else if (response.statusCode < 500) {
      throw CasinoApiException.clientError(message: response.body);
    } else {
      throw CasinoApiException.serverError(message: response.body);
    }
  }
}