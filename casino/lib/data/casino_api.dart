import 'dart:convert';

import 'package:freezed_annotation/freezed_annotation.dart';
import 'package:http/http.dart';
import 'package:logger/logger.dart';

import '../utils/uri_extensions.dart';
import 'app_settings.dart';

part 'casino_api.freezed.dart';
part 'casino_api.g.dart';

@freezed
class SlotMachine with _$SlotMachine {
  const factory SlotMachine.create({
    required String id,
    required String name,
    required int tokens,
  }) = _SlotMachine;

  factory SlotMachine.fromJson(Map<String, dynamic> json) =>
      _$SlotMachineFromJson(json);
}

class CasinoApi {
  const CasinoApi({
    required this.http,
    required this.config,
    required this.logger,
  });

  final Client http;
  final AppSettings config;
  final Logger logger;

  Uri get uri => config.apiUri.addPathSegments(['casino']);

  Future<SlotMachine> addSlotMachine(String name) async {
    logger.i('adding slot-machine with name $name');
    final response = await http.post(
      uri.addPathSegments(['slot-machines']),
      body: jsonEncode({'name': name}),
    );
    await validateResponse(response);
    return SlotMachine.create(
      id: response.body,
      name: name,
      tokens: 0,
    );
  }

  Future<List<SlotMachine>> listSlotMachines() async {
    logger.d('listing slot-machines');
    final response = await http.get(
      uri.addPathSegments(['slot-machines']),
    );
    await validateResponse(response);
    final list = jsonDecode(response.body) as List;
    return list
        .cast<Map<String, dynamic>>()
        .map((json) => SlotMachine.fromJson(json))
        .toList();
  }

  Future<int> getTokens(String id) async {
    logger.d('getting tokens of slot-machine $id');
    final response = await http.get(uri.addPathSegments(
      ['slot-machines', id, 'tokens'],
    ));
    await validateResponse(response);
    return int.parse(response.body);
  }

  Future<void> setTokens(String id, int count) async {
    assert(count >= 0);
    logger.i('setting tokens of slot-machine $id to $count');
    final response = await http.put(uri.replace(
      pathSegments: [...uri.pathSegments, 'slot-machines', id, 'tokens'],
      queryParameters: {'count': count.toString()},
    ));
    await validateResponse(response);
  }

  Future<void> removeSlotMachine(String id) async {
    logger.i('removing slot-machine $id');
    final response = await http.delete(uri.addPathSegments(
      ['slot-machines', id],
    ));
    await validateResponse(response);
  }

  Future<void> validateResponse(Response response) async {
    if (response.statusCode >= 300) {
      final message =
          StringBuffer('Error while sending request to casino api:');
      if (response.request != null) {
        message.write('\n\tRequest:'
            '\n\t-uri: ${response.request!.url.toString()}');
      }
      message.write('\n\tResponse:'
          '\n\t-statusCode: ${response.statusCode}'
          '\n\t-headers: ${response.headers}'
          '\n\t-body: ${response.body}');
      logger.e(message.toString(), response.body, StackTrace.current);
      throw response.body;
    }
  }
}